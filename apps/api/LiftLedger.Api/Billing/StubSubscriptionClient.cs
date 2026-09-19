using Microsoft.Extensions.Options;

namespace LiftLedger.Api.Billing;

/// <summary>
/// Local/CI stand-in for the QckApp Subscription API. Every tenant is treated as active.
/// </summary>
public sealed class StubSubscriptionClient : ISubscriptionClient
{
    private readonly SubscriptionApiOptions _options;
    private readonly ILogger<StubSubscriptionClient> _logger;

    public StubSubscriptionClient(IOptions<SubscriptionApiOptions> options, ILogger<StubSubscriptionClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<EntitlementsDto> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new EntitlementsDto
        {
            ProductCode = _options.ProductCode,
            TenantId = tenantId.ToString(),
            Status = "active",
            PlanCode = "stub",
            PlanName = "Stub (local/CI)"
        });
    }

    public Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Stub subscription API: upsert tenant {ExternalTenantId} ({Name})",
            request.ExternalTenantId,
            request.Name);
        return Task.CompletedTask;
    }

    public Task<BillingSessionDto> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new BillingSessionDto
        {
            Url = $"https://billing.stub.local/checkout/{Uri.EscapeDataString(request.ExternalTenantId)}"
        });
    }

    public Task<BillingSessionDto> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new BillingSessionDto
        {
            Url = $"https://billing.stub.local/portal/{Uri.EscapeDataString(request.ExternalTenantId)}"
        });
    }
}
