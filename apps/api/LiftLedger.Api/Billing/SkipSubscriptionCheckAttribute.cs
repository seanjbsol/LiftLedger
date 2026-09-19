namespace LiftLedger.Api.Billing;

/// <summary>Marks an endpoint that must remain reachable without an active subscription (checkout, portal, entitlements).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipSubscriptionCheckAttribute : Attribute
{
}
