using System.Text.Json;
using LiftLedger.Api.Auth;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiftLedger.Api.Services;

public class InspectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly CertificateService _certificates;

    public InspectionService(AppDbContext db, ICurrentUser currentUser, CertificateService certificates)
    {
        _db = db;
        _currentUser = currentUser;
        _certificates = certificates;
    }

    public async Task<IReadOnlyList<InspectionSummary>> ListAsync(Guid? assetId, CancellationToken cancellationToken)
    {
        var query = _db.Inspections.AsNoTracking()
            .Include(i => i.Asset)
            .Include(i => i.Examiner)
            .Include(i => i.Certificates)
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

        var asset = await _db.Assets
                        .Include(a => a.Client)
                        .Include(a => a.Site)
                        .Include(a => a.Tenant)
                        .FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken)
                    ?? throw new KeyNotFoundException("Asset was not found.");
        _currentUser.EnsureTenant(asset);

        var lastExam = await _db.Inspections.AsNoTracking()
            .Where(i => i.AssetId == asset.Id && i.Status == InspectionStatus.Completed)
            .OrderByDescending(i => i.ExaminationDate)
            .Select(i => (DateOnly?)i.ExaminationDate)
            .FirstOrDefaultAsync(cancellationToken);

        var employer = asset.Client;
        var premises = asset.Site;
        var tenant = asset.Tenant;

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
            DateOfLastExamination = lastExam,
            SafeWorkingLoad = asset.SafeWorkingLoad,
            ExaminerQualifications = TrimToNull(request.ExaminerQualifications),
            EmployerName = employer?.Name ?? tenant.Name,
            EmployerAddress = FormatAddress(employer?.AddressLine1, employer?.Town, employer?.Postcode)
                              ?? FormatAddress(tenant.AddressLine1, tenant.Town, tenant.Postcode),
            PremisesAddress = FormatAddress(premises?.AddressLine1, premises?.Town, premises?.Postcode)
                              ?? FormatAddress(tenant.AddressLine1, tenant.Town, tenant.Postcode),
            CompetentPersonAddress = FormatAddress(tenant.AddressLine1, tenant.Town, tenant.Postcode),
            PuwerTemplateCode = request.ExaminationType == ExaminationType.PuwerInspection
                ? PuwerTemplates.ForCategory(asset.Category).Code
                : null
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
                             .Include(i => i.Examiner)
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

        if (inspection.ExaminationType == ExaminationType.PuwerInspection)
        {
            var template = PuwerTemplates.Find(request.PuwerTemplateCode)
                           ?? PuwerTemplates.ForCategory(inspection.Asset.Category);
            inspection.PuwerTemplateCode = template.Code;
            inspection.PuwerAnswersJson = JsonSerializer.Serialize(request.PuwerAnswers, JsonOptions);
        }

        var prefix = inspection.ExaminationType == ExaminationType.PuwerInspection ? $"PW-{DateTime.UtcNow.Year}-" : $"LL-{DateTime.UtcNow.Year}-";

        inspection.Result = request.Result;
        inspection.ExaminationDate = request.ExaminationDate ?? inspection.ExaminationDate;
        inspection.NextDueDate = request.NextDueDate;
        inspection.DateOfLastExamination = request.DateOfLastExamination ?? inspection.DateOfLastExamination;
        inspection.DateOfReport = request.DateOfReport ?? DateOnly.FromDateTime(DateTime.UtcNow);
        inspection.SafeWorkingLoad = TrimToNull(request.SafeWorkingLoad) ?? inspection.Asset.SafeWorkingLoad;
        inspection.ParticularsOfExamination = TrimToNull(request.ParticularsOfExamination);
        inspection.ParticularsOfTests = TrimToNull(request.ParticularsOfTests);
        inspection.ParticularsOfDefects = TrimToNull(request.ParticularsOfDefects);
        inspection.RepairsRequired = TrimToNull(request.RepairsRequired);
        inspection.ExaminerQualifications = TrimToNull(request.ExaminerQualifications) ?? inspection.ExaminerQualifications;
        inspection.EmployerName = TrimToNull(request.EmployerName) ?? inspection.EmployerName;
        inspection.EmployerAddress = TrimToNull(request.EmployerAddress) ?? inspection.EmployerAddress;
        inspection.PremisesAddress = TrimToNull(request.PremisesAddress) ?? inspection.PremisesAddress;
        inspection.CompetentPersonAddress = TrimToNull(request.CompetentPersonAddress) ?? inspection.CompetentPersonAddress;
        inspection.AuthenticatingPerson = TrimToNull(request.AuthenticatingPerson) ?? inspection.Examiner.FullName;
        inspection.Declaration = TrimToNull(request.Declaration);
        inspection.Status = InspectionStatus.Completed;
        inspection.CompletedAt = DateTime.UtcNow;
        inspection.CertificateNumber ??= await NextCertificateNumberAsync(prefix, cancellationToken);

        if (inspection.Defects.Count > 0)
        {
            _db.Defects.RemoveRange(inspection.Defects);
        }

        var createdDefects = new List<Defect>();
        foreach (var input in request.Defects)
        {
            var defect = CreateDefect(inspection, input);
            createdDefects.Add(defect);
            _db.Defects.Add(defect);
        }

        inspection.Asset.NextExaminationDue = request.NextDueDate;
        inspection.Asset.UpdatedAt = DateTime.UtcNow;
        if (request.Result == InspectionResult.Fail
            || createdDefects.Any(d => d.RequiresImmediateWithdrawal))
        {
            inspection.Asset.Notes = AppendNote(
                inspection.Asset.Notes,
                $"Withdrawn from service pending repair after examination {inspection.CertificateNumber}.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        var reloaded = await QueryDetails()
                           .Include(i => i.Tenant)
                           .FirstOrDefaultAsync(i => i.Id == inspection.Id, cancellationToken)
                       ?? inspection;
        await _certificates.TryIssueAsync(reloaded, cancellationToken);
        return await GetAsync(inspection.Id, cancellationToken);
    }

    internal Defect CreateDefect(Inspection inspection, DefectInput input) =>
        new()
        {
            TenantId = _currentUser.TenantId,
            InspectionId = inspection.Id,
            AssetId = inspection.AssetId,
            Description = input.Description.Trim(),
            Severity = input.Severity,
            Status = DefectStatus.Open,
            Category = TrimToNull(input.Category),
            RequiresImmediateWithdrawal = input.RequiresImmediateWithdrawal
                                          || input.Severity == DefectSeverity.ImmediateDanger,
            Notes = TrimToNull(input.Notes)
        };

    private async Task<string> NextCertificateNumberAsync(string prefix, CancellationToken cancellationToken)
    {
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
            .Include(i => i.Defects).ThenInclude(d => d.AssignedTo)
            .Include(i => i.Defects).ThenInclude(d => d.Photos)
            .Include(i => i.Certificates);

    internal static InspectionDetail ToDetail(Inspection inspection)
    {
        var answers = ParseAnswers(inspection.PuwerAnswersJson);
        var storedId = inspection.Certificates.OrderByDescending(c => c.IssuedAt).FirstOrDefault()?.Id;
        return new InspectionDetail(
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
            inspection.DateOfLastExamination,
            inspection.DateOfReport,
            inspection.SafeWorkingLoad,
            inspection.ParticularsOfExamination,
            inspection.ParticularsOfTests,
            inspection.ParticularsOfDefects,
            inspection.RepairsRequired,
            inspection.ExaminerQualifications,
            inspection.EmployerName,
            inspection.EmployerAddress,
            inspection.PremisesAddress,
            inspection.CompetentPersonAddress,
            inspection.AuthenticatingPerson,
            inspection.Declaration,
            inspection.CertificateNumber,
            inspection.PuwerTemplateCode,
            answers,
            storedId,
            inspection.CreatedAt,
            inspection.CompletedAt,
            inspection.Defects.Select(DefectService.ToDto).ToList());
    }

    private static IReadOnlyList<PuwerAnswerDto> ParseAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PuwerAnswerDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FormatAddress(string? line, string? town, string? postcode)
    {
        var parts = new[] { line, town, postcode }.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

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
