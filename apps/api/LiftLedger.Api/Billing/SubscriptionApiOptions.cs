namespace LiftLedger.Api.Billing;

public sealed class SubscriptionApiOptions
{
    public const string SectionName = "SubscriptionApi";

    /// <summary>Base URL of the central QckApp Subscription API (no trailing path).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Service-to-service API key. Configure via SubscriptionApi__ApiKey; do not commit secrets.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string ProductCode { get; set; } = "LiftLedger";

    /// <summary>
    /// When true, tenants are treated as active and checkout/portal URLs are local stubs.
    /// Use for tests, CI and local development without Qck.
    /// </summary>
    public bool UseStub { get; set; }

    /// <summary>Header name sent to Qck with <see cref="ApiKey"/>.</summary>
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
}
