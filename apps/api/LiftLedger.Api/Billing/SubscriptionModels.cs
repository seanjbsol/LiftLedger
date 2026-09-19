using System.Text.Json.Serialization;

namespace LiftLedger.Api.Billing;

public sealed class EntitlementsDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Qck subscription status, e.g. active, trialing, past_due, canceled, none.</summary>
    public string Status { get; set; } = "none";

    public string? PlanCode { get; set; }
    public string? PlanName { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public string? CheckoutUrl { get; set; }

    [JsonIgnore]
    public bool HasAccess =>
        Status.Equals("active", StringComparison.OrdinalIgnoreCase)
        || Status.Equals("trialing", StringComparison.OrdinalIgnoreCase)
        || Status.Equals("trial", StringComparison.OrdinalIgnoreCase);
}

public sealed class UpsertTenantRequest
{
    public string ProductCode { get; init; } = string.Empty;
    public string ExternalTenantId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
}

public sealed class CreateCheckoutSessionRequest
{
    public string ProductCode { get; init; } = string.Empty;
    public string ExternalTenantId { get; init; } = string.Empty;
    public string? CustomerEmail { get; init; }
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
}

public sealed class CreatePortalSessionRequest
{
    public string ProductCode { get; init; } = string.Empty;
    public string ExternalTenantId { get; init; } = string.Empty;
    public string? ReturnUrl { get; init; }
}

public sealed class BillingSessionDto
{
    public string Url { get; set; } = string.Empty;
}

internal sealed class QckSessionResponse
{
    public string? Url { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? PortalUrl { get; set; }
}

internal sealed class QckEntitlementsResponse
{
    public string? ProductCode { get; set; }
    public string? TenantId { get; set; }
    public string? ExternalTenantId { get; set; }
    public string? Status { get; set; }
    public string? SubscriptionStatus { get; set; }
    public string? PlanCode { get; set; }
    public string? PlanName { get; set; }
    public string? Plan { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public string? CheckoutUrl { get; set; }
}
