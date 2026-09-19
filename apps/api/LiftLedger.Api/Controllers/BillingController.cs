using LiftLedger.Api.Auth;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LiftLedger.Api.Controllers;

[ApiController]
[Authorize]
[SkipSubscriptionCheck]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly ISubscriptionClient _subscriptions;
    private readonly ICurrentUser _currentUser;
    private readonly SubscriptionApiOptions _options;

    public BillingController(
        ISubscriptionClient subscriptions,
        ICurrentUser currentUser,
        IOptions<SubscriptionApiOptions> options)
    {
        _subscriptions = subscriptions;
        _currentUser = currentUser;
        _options = options.Value;
    }

    /// <summary>Current plan/status for the signed-in organisation (used by mobile Settings).</summary>
    [HttpGet("entitlements")]
    public async Task<ActionResult<BillingEntitlementsResponse>> Entitlements(CancellationToken cancellationToken)
    {
        var entitlements = await _subscriptions.GetEntitlementsAsync(_currentUser.TenantId, cancellationToken);
        var plan = PlanCatalog.From(entitlements);
        return Ok(new BillingEntitlementsResponse(
            entitlements.ProductCode,
            _currentUser.TenantId,
            entitlements.Status,
            entitlements.PlanCode,
            entitlements.PlanName,
            entitlements.CurrentPeriodEnd,
            entitlements.HasAccess,
            plan.PlanTier,
            plan.IsPro,
            plan.CanUseCertificates,
            plan.CanUseDefects,
            plan.CanUseClientPortal));
    }

    /// <summary>Proxies to Qck create-checkout-session. Owner/Admin only.</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<BillingSessionResponse>> Checkout(
        CreateCheckoutRequest? request,
        CancellationToken cancellationToken)
    {
        _currentUser.EnsureManageAccess();
        var session = await _subscriptions.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest
            {
                ProductCode = _options.ProductCode,
                ExternalTenantId = _currentUser.TenantId.ToString(),
                CustomerEmail = _currentUser.Email,
                SuccessUrl = request?.SuccessUrl,
                CancelUrl = request?.CancelUrl
            },
            cancellationToken);
        return Ok(new BillingSessionResponse(session.Url));
    }

    /// <summary>Proxies to Qck customer-portal session. Owner/Admin only.</summary>
    [HttpPost("portal")]
    public async Task<ActionResult<BillingSessionResponse>> Portal(
        CreatePortalRequest? request,
        CancellationToken cancellationToken)
    {
        _currentUser.EnsureManageAccess();
        var session = await _subscriptions.CreatePortalSessionAsync(
            new CreatePortalSessionRequest
            {
                ProductCode = _options.ProductCode,
                ExternalTenantId = _currentUser.TenantId.ToString(),
                ReturnUrl = request?.ReturnUrl
            },
            cancellationToken);
        return Ok(new BillingSessionResponse(session.Url));
    }
}
