using LiftLedger.Api.Auth;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiftLedger.Api.Services;

public class InspectionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public InspectionService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InspectionSummary>> ListAsync(Guid? assetId, CancellationToken cancellationToken)
    {
        var query = _db.Inspections.AsNoTracking()
            .Include(i => i.Asset)
            .Include(i => i.Examiner)
            .AsQueryable();

        if (assetId is not null)
        {
            query = query.Where(i => i.AssetId == assetId);
        }

        var items = await query
            .OrderByDescending(i => i.ExaminationDate)
            .ThenByDescending(i => i.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return items.Select(i => Mapping.ToSummary(i)).ToList();
    }

    public async Task<InspectionDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await QueryDetails().FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
                         ?? throw new KeyNotFoundException("Inspection was not found.");
        _currentUser.EnsureTenant(inspection);
        return ToDetail(inspection);
    }

    public async Task<InspectionDetail> StartAsync(StartInspectionRequest request, CancellationToken cancellationToken)
    {
        _currentUser.EnsureWriteAccess();

        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken)
                    ?? throw new KeyNotFoundException("Asset was not found.");
        _currentUser.EnsureTenant(asset);

        var inspection = new Inspection
        {
            TenantId = _currentUser.TenantId,
            AssetId = asset.Id,
            ExaminerUserId = _currentUser.UserId,
            ClientId = asset.ClientId,
            SiteId = asset.SiteId,
            ExaminationType = request.ExaminationType,
            Status = InspectionStatus.Draft,
            ExaminationDate = request.ExaminationDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            SafeWorkingLoad = asset.SafeWorkingLoad,
            ExaminerQualifications = TrimToNull(request.ExaminerQualifications)
        };

        _db.Inspections.Add(inspection);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(inspection.Id, cancellationToken);
    }

    public async Task<InspectionDetail> CompleteAsync(
        Guid id,
        CompleteInspectionRequest request,
        CancellationToken cancellationToken)
    {
        _currentUser.EnsureWriteAccess();

        var inspection = await _db.Inspections
                             .Include(i => i.Defects)
                             .Include(i => i.Asset)
                             .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
                         ?? throw new KeyNotFoundException("Inspection was not found.");
        _currentUser.EnsureTenant(inspection);

        if (inspection.Status == InspectionStatus.Completed)
        {
            throw new InvalidOperationException("This examination has already been completed.");
        }

        if (request.Result != InspectionResult.Pass && request.Defects.Count == 0
            && string.IsNullOrWhiteSpace(request.ParticularsOfDefects))
        {
            throw new ArgumentException("Record at least one defect (or particulars of defects) for a fail or pass-with-defects result.");
        }

        inspection.Result = request.Result;
        inspection.ExaminationDate = request.ExaminationDate ?? inspection.ExaminationDate;
        inspection.NextDueDate = request.NextDueDate;
        inspection.SafeWorkingLoad = TrimToNull(request.SafeWorkingLoad) ?? inspection.Asset.SafeWorkingLoad;
        inspection.ParticularsOfExamination = TrimToNull(request.ParticularsOfExamination);
        inspection.ParticularsOfDefects = TrimToNull(request.ParticularsOfDefects);
        inspection.RepairsRequired = TrimToNull(request.RepairsRequired);
        inspection.ExaminerQualifications = TrimToNull(request.ExaminerQualifications) ?? inspection.ExaminerQualifications;
        inspection.Status = InspectionStatus.Completed;
        inspection.CompletedAt = DateTime.UtcNow;
        inspection.CertificateNumber ??= await NextCertificateNumberAsync(cancellationToken);

        _db.Defects.RemoveRange(inspection.Defects);
        foreach (var input in request.Defects)
        {
            inspection.Defects.Add(new Defect
            {
                TenantId = _currentUser.TenantId,
                InspectionId = inspection.Id,
                AssetId = inspection.AssetId,
                Description = input.Description.Trim(),
                Severity = input.Severity,
                Category = TrimToNull(input.Category),
                RequiresImmediateWithdrawal = input.RequiresImmediateWithdrawal
                    || input.Severity == DefectSeverity.ImmediateDanger,
                Notes = TrimToNull(input.Notes)
            });
        }

        inspection.Asset.NextExaminationDue = request.NextDueDate;
        inspection.Asset.UpdatedAt = DateTime.UtcNow;
        if (request.Result == InspectionResult.Fail
            || inspection.Defects.Any(d => d.RequiresImmediateWithdrawal))
        {
            inspection.Asset.Notes = AppendNote(
                inspection.Asset.Notes,
                $"Withdrawn from service pending repair after examination {inspection.CertificateNumber}.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(inspection.Id, cancellationToken);
    }

    private async Task<string> NextCertificateNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"LL-{year}-";
        var existing = await _db.Inspections.IgnoreQueryFilters()
            .Where(i => i.TenantId == _currentUser.TenantId && i.CertificateNumber != null && i.CertificateNumber.StartsWith(prefix))
            .Select(i => i.CertificateNumber!)
            .ToListAsync(cancellationToken);

        var next = existing
            .Select(value => int.TryParse(value[prefix.Length..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{next:0000}";
    }

    private IQueryable<Inspection> QueryDetails() =>
        _db.Inspections
            .Include(i => i.Asset)
            .Include(i => i.Examiner)
            .Include(i => i.Defects);

    private static InspectionDetail ToDetail(Inspection inspection) =>
        new(
            inspection.Id,
            inspection.TenantId,
            inspection.AssetId,
            inspection.Asset.AssetNumber,
            inspection.Asset.Name,
            inspection.ExaminerUserId,
            inspection.Examiner.FullName,
            inspection.ExaminationType,
            inspection.Status,
            inspection.Result,
            inspection.ExaminationDate,
            inspection.NextDueDate,
            inspection.SafeWorkingLoad,
            inspection.ParticularsOfExamination,
            inspection.ParticularsOfDefects,
            inspection.RepairsRequired,
            inspection.ExaminerQualifications,
            inspection.CertificateNumber,
            inspection.CreatedAt,
            inspection.CompletedAt,
            inspection.Defects.Select(d => new DefectDto(
                d.Id,
                d.Description,
                d.Severity,
                d.Category,
                d.RequiresImmediateWithdrawal,
                d.RectifiedAt,
                d.Notes)).ToList());

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string AppendNote(string? existing, string addition)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return addition;
        }

        if (existing.Contains(addition, StringComparison.OrdinalIgnoreCase))
        {
            return existing;
        }

        return existing.Trim() + " " + addition;
    }
}
