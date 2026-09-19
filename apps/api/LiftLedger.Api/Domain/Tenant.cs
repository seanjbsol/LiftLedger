namespace LiftLedger.Api.Domain;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? TradingName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? Town { get; set; }
    public string? Postcode { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
