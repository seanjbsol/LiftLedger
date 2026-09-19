namespace LiftLedger.Api.Billing;

public interface ISubscriptionClient
{
    Task<EntitlementsDto> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken);

    Task<BillingSessionDto> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken);

    Task<BillingSessionDto> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken);
}
