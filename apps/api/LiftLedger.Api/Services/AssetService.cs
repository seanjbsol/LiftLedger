using LiftLedger.Api.Auth;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using LiftLedger.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace LiftLedger.Api.Services;

public class AssetService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AssetService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AssetSummary>> ListAsync(string? search, CancellationToken cancellationToken)
    {
        return await ListInternalAsync(search, cancellationToken);
    }

    public async Task<IReadOnlyList<AssetSummary>> ListInternalAsync(string? search, CancellationToken cancellationToken)
    {
        var query = _db.Assets.AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.Site)
            .Where(a => !a.IsArchived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.AssetNumber.ToLower().Contains(term)
                || a.Name.ToLower().Contains(term)
                || (a.IdentificationCode != null && a.IdentificationCode.ToLower().Contains(term))
                || (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(term)));
        }

        var assets = await query.OrderBy(a => a.AssetNumber).ToListAsync(cancellationToken);
        return assets.Select(MapSummary).ToList();
    }

    public async Task<AssetDetail> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await _db.Assets.AsNoTracking()
                        .Include(a => a.Client)
                        .Include(a => a.Site)
                        .Include(a => a.Inspections).ThenInclude(i => i.Examiner)
                        .Include(a => a.Inspections).ThenInclude(i => i.Certificates)
                        .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                    ?? throw new KeyNotFoundException("Asset was not found.");

        _currentUser.EnsureTenant(asset);

        var inspections = asset.Inspections
            .OrderByDescending(i => i.ExaminationDate)
            .ThenByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => Mapping.ToSummary(i, asset))
            .ToList();

        return Mapping.ToDetail(asset, inspections);
    }

    public async Task<AssetDetail> CreateAsync(UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        _currentUser.EnsureWriteAccess();
        await EnsureUniqueNumber(request.AssetNumber, null, cancellationToken);
        await EnsureRelated(request.ClientId, request.SiteId, cancellationToken);

        var asset = new Asset
        {
            TenantId = _currentUser.TenantId,
            AssetNumber = request.AssetNumber.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Category = request.Category,
            Make = TrimToNull(request.Make),
            Model = TrimToNull(request.Model),
            SerialNumber = TrimToNull(request.SerialNumber),
            IdentificationCode = TrimToNull(request.IdentificationCode),
            SafeWorkingLoad = TrimToNull(request.SafeWorkingLoad),
            FirstUseDate = request.FirstUseDate,
            NextExaminationDue = request.NextExaminationDue,
            Notes = TrimToNull(request.Notes),
            ClientId = request.ClientId,
            SiteId = request.SiteId
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(asset.Id, cancellationToken);
    }

    public async Task<AssetDetail> UpdateAsync(Guid id, UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        _currentUser.EnsureWriteAccess();
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                    ?? throw new KeyNotFoundException("Asset was not found.");
        _currentUser.EnsureTenant(asset);

        await EnsureUniqueNumber(request.AssetNumber, id, cancellationToken);
        await EnsureRelated(request.ClientId, request.SiteId, cancellationToken);

        asset.AssetNumber = request.AssetNumber.Trim().ToUpperInvariant();
        asset.Name = request.Name.Trim();
        asset.Category = request.Category;
        asset.Make = TrimToNull(request.Make);
        asset.Model = TrimToNull(request.Model);
        asset.SerialNumber = TrimToNull(request.SerialNumber);
        asset.IdentificationCode = TrimToNull(request.IdentificationCode);
        asset.SafeWorkingLoad = TrimToNull(request.SafeWorkingLoad);
        asset.FirstUseDate = request.FirstUseDate;
        asset.NextExaminationDue = request.NextExaminationDue;
        asset.Notes = TrimToNull(request.Notes);
        asset.ClientId = request.ClientId;
        asset.SiteId = request.SiteId;
        asset.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(asset.Id, cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        _currentUser.EnsureManageAccess();
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                    ?? throw new KeyNotFoundException("Asset was not found.");
        _currentUser.EnsureTenant(asset);
        asset.IsArchived = true;
        asset.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssetScanResponse> ResolveByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var term = code.Trim();
        if (term.StartsWith("liftledger://", StringComparison.OrdinalIgnoreCase))
        {
            var slash = term.LastIndexOf('/');
            term = slash >= 0 ? term[(slash + 1)..] : term;
        }

        term = Uri.UnescapeDataString(term);
        var lowered = term.ToLower();

        var asset = await _db.Assets.AsNoTracking()
                        .Include(a => a.Inspections).ThenInclude(i => i.Certificates)
                        .Where(a => !a.IsArchived)
                        .FirstOrDefaultAsync(
                            a => (a.IdentificationCode != null && a.IdentificationCode.ToLower() == lowered)
                                 || a.AssetNumber.ToLower() == lowered,
                            cancellationToken)
                    ?? throw new KeyNotFoundException("No asset matched that QR or identification code.");

        _currentUser.EnsureTenant(asset);

        var last = asset.Inspections
            .Where(i => i.Status == InspectionStatus.Completed)
            .OrderByDescending(i => i.ExaminationDate)
            .ThenByDescending(i => i.CompletedAt)
            .FirstOrDefault();

        var lastCert = last?.Certificates.OrderByDescending(c => c.IssuedAt).FirstOrDefault();
        var payload = asset.IdentificationCode ?? asset.AssetNumber;

        return new AssetScanResponse(
            asset.Id,
            asset.AssetNumber,
            asset.Name,
            asset.Category,
            asset.IdentificationCode,
            DeepLinkFor(payload),
            last?.Id,
            lastCert?.Id,
            last?.CertificateNumber,
            last?.ExaminationDate);
    }

    public async Task<(byte[] Png, string FileName)> QrPngAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                    ?? throw new KeyNotFoundException("Asset was not found.");
        _currentUser.EnsureTenant(asset);

        var payload = DeepLinkFor(asset.IdentificationCode ?? asset.AssetNumber);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return (png, $"{asset.AssetNumber}-qr.png");
    }

    public static string DeepLinkFor(string code) => $"liftledger://a/{Uri.EscapeDataString(code)}";

    private async Task EnsureUniqueNumber(string assetNumber, Guid? excludeId, CancellationToken cancellationToken)
    {
        var number = assetNumber.Trim().ToUpperInvariant();
        var exists = await _db.Assets.AnyAsync(
            a => a.AssetNumber == number && (excludeId == null || a.Id != excludeId),
            cancellationToken);
        if (exists)
        {
            throw new ConflictException("An asset with this fleet number already exists in your organisation.");
        }
    }

    private async Task EnsureRelated(Guid? clientId, Guid? siteId, CancellationToken cancellationToken)
    {
        if (clientId is not null)
        {
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken)
                         ?? throw new KeyNotFoundException("Client was not found.");
            _currentUser.EnsureTenant(client);
        }

        if (siteId is not null)
        {
            var site = await _db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, cancellationToken)
                       ?? throw new KeyNotFoundException("Site was not found.");
            _currentUser.EnsureTenant(site);
        }
    }

    internal static AssetSummary MapSummary(Asset asset) =>
        new(
            asset.Id,
            asset.AssetNumber,
            asset.Name,
            asset.Category,
            asset.IdentificationCode,
            asset.SafeWorkingLoad,
            asset.NextExaminationDue,
            asset.IsArchived,
            asset.Client?.Name,
            asset.Site?.Name,
            DueStatus.For(asset.NextExaminationDue));

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class Mapping
{
    public static InspectionSummary ToSummary(Inspection inspection, Asset? asset = null) =>
        new(
            inspection.Id,
            inspection.AssetId,
            asset?.AssetNumber ?? inspection.Asset?.AssetNumber ?? string.Empty,
            asset?.Name ?? inspection.Asset?.Name ?? string.Empty,
            inspection.ExaminationType,
            inspection.Status,
            inspection.Result,
            inspection.ExaminationDate,
            inspection.NextDueDate,
            inspection.CertificateNumber,
            inspection.Examiner?.FullName ?? string.Empty,
            inspection.Certificates?.OrderByDescending(c => c.IssuedAt).FirstOrDefault()?.Id);

    public static AssetDetail ToDetail(Asset asset, IReadOnlyList<InspectionSummary> inspections) =>
        new(
            asset.Id,
            asset.TenantId,
            asset.AssetNumber,
            asset.Name,
            asset.Category,
            asset.Make,
            asset.Model,
            asset.SerialNumber,
            asset.IdentificationCode,
            asset.SafeWorkingLoad,
            asset.FirstUseDate,
            asset.NextExaminationDue,
            asset.Notes,
            asset.IsArchived,
            asset.ClientId,
            asset.SiteId,
            asset.Client?.Name,
            asset.Site?.Name,
            DueStatus.For(asset.NextExaminationDue),
            inspections);
}
