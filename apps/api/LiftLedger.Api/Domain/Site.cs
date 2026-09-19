namespace LiftLedger.Api.Domain;

public class Site : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? Town { get; set; }
    public string? Postcode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Client? Client { get; set; }
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
