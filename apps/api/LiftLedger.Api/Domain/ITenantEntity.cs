namespace LiftLedger.Api.Domain;

/// <summary>
/// Every business record belongs to a workshop/hire tenant.
/// Combined with EF global query filters and explicit service checks.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
