namespace LiftLedger.Api.Billing;

public sealed class PaywallException : Exception
{
    public PaywallException(string feature, string message) : base(message)
    {
        Feature = feature;
    }

    public string Feature { get; }
    public string RequiredPlan { get; } = PlanTiers.Pro;
}
