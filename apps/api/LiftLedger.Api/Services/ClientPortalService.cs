using System.Security.Cryptography;
using LiftLedger.Api.Auth;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiftLedger.Api.Services;

public class ClientPortalService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFeatureGate _features;

    public ClientPortalService(AppDbContext db, ICurrentUser currentUser, IFeatureGate features)
    {
        _db = db;
        _currentUser = currentUser;
        _features = features;
    }

    public async Task<IReadOnlyList<PublicPortalCertificate>> ListForCurrentTenantAsync(CancellationToken cancellationToken)
    {
        await _features.EnsureClientPortalAsync(cancellationToken);
        var items = await _db.Certificates.AsNoTracking()
            .Include(c => c.Asset)
            .OrderByDescending(c => c.IssuedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return items.Select(ToPublic).ToList();
    }

    public async Task<PortalTokenResponse> CreateTokenAsync(CreatePortalTokenRequest request, CancellationToken cancellationToken)
    {
        await _features.EnsureClientPortalAsync(cancellationToken);
        _currentUser.EnsureManageAccess();

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken)
                     ?? throw new KeyNotFoundException("Client was not found.");
        _currentUser.EnsureTenant(client);

        var days = request.ValidDays is > 0 and <= 365 ? request.ValidDays.Value : 90;
        var token = new ClientPortalToken
        {
            TenantId = _currentUser.TenantId,
            ClientId = client.Id,
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            Label = InspectionService.TrimToNull(request.Label) ?? $"{client.Name} download portal",
            ExpiresAt = DateTime.UtcNow.AddDays(days)
        };
        _db.ClientPortalTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
        return ToTokenResponse(token, client.Name);
    }

    public async Task<IReadOnlyList<PortalTokenResponse>> ListTokensAsync(CancellationToken cancellationToken)
    {
        await _features.EnsureClientPortalAsync(cancellationToken);
        var tokens = await _db.ClientPortalTokens.AsNoTracking()
            .Include(t => t.Client)
            .Where(t => t.RevokedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
        return tokens.Select(t => ToTokenResponse(t, t.Client.Name)).ToList();
    }

    public async Task<PublicPortalView> GetPublicAsync(string token, CancellationToken cancellationToken)
    {
        var share = await LoadShareAsync(token, cancellationToken);
        var certificates = await _db.Certificates.IgnoreQueryFilters()
            .AsNoTracking()
            .Include(c => c.Asset)
            .Where(c => c.TenantId == share.TenantId && c.ClientId == share.ClientId)
            .OrderByDescending(c => c.IssuedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new PublicPortalView(
            share.Tenant.Name,
            share.Client.Name,
            "These are LiftLedger working records (Schedule 1 style / PUWER assessment notes). They are not HSE-certified and are not a legal determination of compliance.",
            certificates.Select(ToPublic).ToList());
    }

    public async Task<Certificate> GetPublicCertificateAsync(string token, Guid certificateId, CancellationToken cancellationToken)
    {
        var share = await LoadShareAsync(token, cancellationToken);
        var certificate = await _db.Certificates.IgnoreQueryFilters()
                              .Include(c => c.Asset)
                              .FirstOrDefaultAsync(c => c.Id == certificateId && c.TenantId == share.TenantId, cancellationToken)
                          ?? throw new KeyNotFoundException("Certificate was not found.");

        if (certificate.ClientId != share.ClientId)
        {
            throw new KeyNotFoundException("Certificate was not found.");
        }

        return certificate;
    }

    private async Task<ClientPortalToken> LoadShareAsync(string token, CancellationToken cancellationToken)
    {
        var share = await _db.ClientPortalTokens.IgnoreQueryFilters()
                        .Include(t => t.Tenant)
                        .Include(t => t.Client)
                        .FirstOrDefaultAsync(t => t.Token == token, cancellationToken)
                    ?? throw new KeyNotFoundException("This download link is not valid.");

        if (share.RevokedAt is not null || share.ExpiresAt < DateTime.UtcNow)
        {
            throw new KeyNotFoundException("This download link has expired.");
        }

        return share;
    }

    private static PublicPortalCertificate ToPublic(Certificate certificate) =>
        new(
            certificate.Id,
            certificate.CertificateNumber,
            certificate.Asset?.AssetNumber ?? string.Empty,
            certificate.Asset?.Name ?? string.Empty,
            certificate.Kind,
            certificate.IssuedAt);

    private static PortalTokenResponse ToTokenResponse(ClientPortalToken token, string clientName) =>
        new(
            token.Id,
            token.ClientId,
            clientName,
            token.Token,
            $"/api/public/portal/{token.Token}",
            token.ExpiresAt);
}
