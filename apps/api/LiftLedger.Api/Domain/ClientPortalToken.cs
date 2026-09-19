namespace LiftLedger.Api.Domain;

/// <summary>
/// Share token for the client download portal stub. A hire customer can open
/// the last stored certificates for their assets without a LiftLedger login.
/// </summary>
public class ClientPortalToken : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Label { get; set; } = "Client download portal";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Client Client { get; set; } = null!;
}
