using LiftLedger.Api.Auth;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiftLedger.Api.Services;

public class ClientService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ClientService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ClientDto>> ListClientsAsync(CancellationToken cancellationToken)
    {
        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return clients.Select(MapClient).ToList();
    }

    public async Task<ClientDto> CreateClientAsync(UpsertClientRequest request, CancellationToken cancellationToken)
    {
        _currentUser.EnsureManageAccess();
        var client = new Client
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name.Trim(),
            ContactName = TrimToNull(request.ContactName),
            Email = TrimToNull(request.Email)?.ToLowerInvariant(),
            Phone = TrimToNull(request.Phone),
            AddressLine1 = TrimToNull(request.AddressLine1),
            Town = TrimToNull(request.Town),
            Postcode = TrimToNull(request.Postcode)?.ToUpperInvariant()
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync(cancellationToken);
        return MapClient(client);
    }

    public async Task<IReadOnlyList<SiteDto>> ListSitesAsync(CancellationToken cancellationToken)
    {
        var sites = await _db.Sites.AsNoTracking()
            .Include(s => s.Client)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
        return sites.Select(MapSite).ToList();
    }

    public async Task<SiteDto> CreateSiteAsync(UpsertSiteRequest request, CancellationToken cancellationToken)
    {
        _currentUser.EnsureManageAccess();
        if (request.ClientId is not null)
        {
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken)
                         ?? throw new KeyNotFoundException("Client was not found.");
            _currentUser.EnsureTenant(client);
        }

        var site = new Site
        {
            TenantId = _currentUser.TenantId,
            ClientId = request.ClientId,
            Name = request.Name.Trim(),
            AddressLine1 = TrimToNull(request.AddressLine1),
            Town = TrimToNull(request.Town),
            Postcode = TrimToNull(request.Postcode)?.ToUpperInvariant()
        };
        _db.Sites.Add(site);
        await _db.SaveChangesAsync(cancellationToken);
        var created = await _db.Sites.Include(s => s.Client).FirstAsync(s => s.Id == site.Id, cancellationToken);
        return MapSite(created);
    }

    private static ClientDto MapClient(Client client) =>
        new(client.Id, client.Name, client.ContactName, client.Email, client.Phone, client.Town, client.Postcode);

    private static SiteDto MapSite(Site site) =>
        new(site.Id, site.ClientId, site.Client?.Name, site.Name, site.AddressLine1, site.Town, site.Postcode);

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
