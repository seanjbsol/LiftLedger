using LiftLedger.Api.Auth;

namespace LiftLedger.Api.Billing;

public interface IFeatureGate
{
    Task<PlanSnapshot> GetAsync(CancellationToken cancellationToken);
    Task EnsureCertificatesAsync(CancellationToken cancellationToken);
    Task EnsureDefectsAsync(CancellationToken cancellationToken);
    Task EnsureClientPortalAsync(CancellationToken cancellationToken);
}

public sealed class FeatureGate : IFeatureGate
{
    public const string EntitlementsItemKey = "SubscriptionEntitlements";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISubscriptionClient _subscriptions;
    private readonly ICurrentUser _currentUser;

    public FeatureGate(
        IHttpContextAccessor httpContextAccessor,
        ISubscriptionClient subscriptions,
        ICurrentUser currentUser)
    {
        _httpContextAccessor = httpContextAccessor;
        _subscriptions = subscriptions;
        _currentUser = currentUser;
    }

    public async Task<PlanSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        if (_httpContextAccessor.HttpContext?.Items[EntitlementsItemKey] is EntitlementsDto cached)
        {
            return PlanCatalog.From(cached);
        }

        var entitlements = await _subscriptions.GetEntitlementsAsync(_currentUser.TenantId, cancellationToken);
        if (_httpContextAccessor.HttpContext is not null)
        {
            _httpContextAccessor.HttpContext.Items[EntitlementsItemKey] = entitlements;
        }

        return PlanCatalog.From(entitlements);
    }

    public Task EnsureCertificatesAsync(CancellationToken cancellationToken) =>
        EnsureProAsync(
            PlanFeatures.Certificates,
            "Certificate builders and stored LOLER/PUWER working records are included on LiftLedger Pro.",
            cancellationToken);

    public Task EnsureDefectsAsync(CancellationToken cancellationToken) =>
        EnsureProAsync(
            PlanFeatures.Defects,
            "The defect workflow (assign, photos, close and retest) is included on LiftLedger Pro.",
            cancellationToken);

    public Task EnsureClientPortalAsync(CancellationToken cancellationToken) =>
        EnsureProAsync(
            PlanFeatures.ClientPortal,
            "The client download portal is included on LiftLedger Pro.",
            cancellationToken);

    private async Task EnsureProAsync(string feature, string message, CancellationToken cancellationToken)
    {
        var plan = await GetAsync(cancellationToken);
        if (!plan.HasAccess)
        {
            throw new PaywallException(
                feature,
                "An active or trial LiftLedger subscription is required for this organisation.");
        }

        if (!plan.IsPro)
        {
            throw new PaywallException(feature, message);
        }
    }
}
