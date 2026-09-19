using LiftLedger.Api.Auth;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiftLedger.Api.Services;

public class DefectService
{
    private static readonly HashSet<string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFeatureGate _features;
    private readonly InspectionService _inspections;

    public DefectService(
        AppDbContext db,
        ICurrentUser currentUser,
        IFeatureGate features,
        InspectionService inspections)
    {
        _db = db;
        _currentUser = currentUser;
        _features = features;
        _inspections = inspections;
    }

    public async Task<IReadOnlyList<DefectDto>> ListAsync(Guid? assetId, DefectStatus? status, CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);

        var query = Query().AsNoTracking();
        if (assetId is not null)
        {
            query = query.Where(d => d.AssetId == assetId);
        }

        if (status is not null)
        {
            query = query.Where(d => d.Status == status);
        }

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToList();
    }

    public async Task<DefectDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);
        var defect = await Query().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                     ?? throw new KeyNotFoundException("Defect was not found.");
        _currentUser.EnsureTenant(defect);
        return ToDto(defect);
    }

    public async Task<DefectDto> RaiseFromInspectionAsync(
        Guid inspectionId,
        RaiseDefectRequest request,
        CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);
        _currentUser.EnsureWriteAccess();

        var inspection = await _db.Inspections
                             .Include(i => i.Asset)
                             .FirstOrDefaultAsync(i => i.Id == inspectionId, cancellationToken)
                         ?? throw new KeyNotFoundException("Inspection was not found.");
        _currentUser.EnsureTenant(inspection);

        if (inspection.Status != InspectionStatus.Completed)
        {
            throw new InvalidOperationException("Raise defects from a completed examination, or record them when completing the form.");
        }

        var defect = _inspections.CreateDefect(inspection, request);
        _db.Defects.Add(defect);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(defect.Id, cancellationToken);
    }

    public async Task<DefectDto> AssignAsync(Guid id, AssignDefectRequest request, CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);
        _currentUser.EnsureWriteAccess();

        var defect = await Query().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                     ?? throw new KeyNotFoundException("Defect was not found.");
        _currentUser.EnsureTenant(defect);
        EnsureOpen(defect);

        var membership = await _db.Memberships.Include(m => m.User)
                             .FirstOrDefaultAsync(m => m.UserId == request.AssignedToUserId, cancellationToken)
                         ?? throw new KeyNotFoundException("That person is not a member of this organisation.");
        _currentUser.EnsureTenant(membership);

        defect.AssignedToUserId = membership.UserId;
        defect.AssignedAt = DateTime.UtcNow;
        defect.Status = DefectStatus.Assigned;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(defect.Id, cancellationToken);
    }

    public async Task<DefectPhotoDto> AddPhotoAsync(
        Guid id,
        DefectPhotoKind kind,
        string fileName,
        string contentType,
        byte[] data,
        CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);
        _currentUser.EnsureWriteAccess();

        if (data.Length == 0)
        {
            throw new ArgumentException("A photo file is required.");
        }

        if (data.Length > 5 * 1024 * 1024)
        {
            throw new ArgumentException("Photos must be 5 MB or smaller.");
        }

        if (!AllowedPhotoTypes.Contains(contentType))
        {
            throw new ArgumentException("Use a JPEG, PNG or WebP photo.");
        }

        var defect = await Query().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                     ?? throw new KeyNotFoundException("Defect was not found.");
        _currentUser.EnsureTenant(defect);
        if (defect.Status is DefectStatus.Closed)
        {
            throw new InvalidOperationException("Photos cannot be added to a closed defect.");
        }

        var photo = new DefectPhoto
        {
            TenantId = _currentUser.TenantId,
            DefectId = defect.Id,
            Kind = kind,
            FileName = string.IsNullOrWhiteSpace(fileName) ? $"{kind.ToString().ToLowerInvariant()}.jpg" : fileName,
            ContentType = contentType,
            Data = data,
            UploadedByUserId = _currentUser.UserId
        };
        _db.DefectPhotos.Add(photo);
        await _db.SaveChangesAsync(cancellationToken);
        return new DefectPhotoDto(photo.Id, photo.Kind, photo.FileName, photo.ContentType, photo.UploadedAt);
    }

    public async Task<DefectPhoto> GetPhotoAsync(Guid defectId, Guid photoId, CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);
        var photo = await _db.DefectPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.DefectId == defectId, cancellationToken)
                    ?? throw new KeyNotFoundException("Photo was not found.");
        _currentUser.EnsureTenant(photo);
        return photo;
    }

    public async Task<DefectDto> CloseAsync(Guid id, CloseDefectRequest request, CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);
        _currentUser.EnsureWriteAccess();

        var defect = await Query().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                     ?? throw new KeyNotFoundException("Defect was not found.");
        _currentUser.EnsureTenant(defect);
        EnsureOpen(defect);

        if (!defect.Photos.Any(p => p.Kind == DefectPhotoKind.Before))
        {
            throw new InvalidOperationException("Attach a before photo before closing the defect.");
        }

        if (!request.RequiresRetest && !defect.Photos.Any(p => p.Kind == DefectPhotoKind.After))
        {
            throw new InvalidOperationException("Attach an after photo to close the defect, or mark that a retest is required.");
        }

        defect.CloseNotes = InspectionService.TrimToNull(request.CloseNotes);
        defect.ClosedByUserId = _currentUser.UserId;
        defect.ClosedAt = DateTime.UtcNow;
        defect.RequiresRetest = request.RequiresRetest;

        if (request.RequiresRetest)
        {
            defect.Status = DefectStatus.RetestRequired;
            defect.RectifiedAt = null;
        }
        else
        {
            defect.Status = DefectStatus.Closed;
            defect.RectifiedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(defect.Id, cancellationToken);
    }

    public async Task<InspectionDetail> StartRetestAsync(Guid id, CancellationToken cancellationToken)
    {
        await _features.EnsureDefectsAsync(cancellationToken);
        _currentUser.EnsureWriteAccess();

        var defect = await Query().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                     ?? throw new KeyNotFoundException("Defect was not found.");
        _currentUser.EnsureTenant(defect);

        if (defect.Status != DefectStatus.RetestRequired && !defect.RequiresRetest)
        {
            throw new InvalidOperationException("A retest can only be started when the defect is marked retest required.");
        }

        var started = await _inspections.StartAsync(
            new StartInspectionRequest
            {
                AssetId = defect.AssetId,
                ExaminationType = ExaminationType.ThoroughExamination
            },
            cancellationToken);

        defect.RetestInspectionId = started.Id;
        await _db.SaveChangesAsync(cancellationToken);
        return started;
    }

    private static void EnsureOpen(Defect defect)
    {
        if (defect.Status is DefectStatus.Closed)
        {
            throw new InvalidOperationException("This defect is already closed.");
        }
    }

    private IQueryable<Defect> Query() =>
        _db.Defects
            .Include(d => d.Asset)
            .Include(d => d.AssignedTo)
            .Include(d => d.Photos)
            .Include(d => d.Inspection);

    internal static DefectDto ToDto(Defect defect) =>
        new(
            defect.Id,
            defect.InspectionId,
            defect.AssetId,
            defect.Asset?.AssetNumber ?? string.Empty,
            defect.Asset?.Name ?? string.Empty,
            defect.Description,
            defect.Severity,
            defect.Status,
            defect.Category,
            defect.RequiresImmediateWithdrawal,
            defect.AssignedToUserId,
            defect.AssignedTo?.FullName,
            defect.AssignedAt,
            defect.RectifiedAt,
            defect.ClosedAt,
            defect.CloseNotes,
            defect.RequiresRetest,
            defect.RetestInspectionId,
            defect.Notes,
            defect.CreatedAt,
            defect.Photos
                .OrderBy(p => p.UploadedAt)
                .Select(p => new DefectPhotoDto(p.Id, p.Kind, p.FileName, p.ContentType, p.UploadedAt))
                .ToList());
}
