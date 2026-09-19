namespace LiftLedger.Api.Domain;

/// <summary>
/// Optional hire-fleet / workshop customer. Not required for in-house plant.
/// </summary>
public class Client : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? Town { get; set; }
    public string? Postcode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Site> Sites { get; set; } = new List<Site>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
