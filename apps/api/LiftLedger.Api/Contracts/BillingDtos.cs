using System.ComponentModel.DataAnnotations;

namespace LiftLedger.Api.Contracts;

public record BillingEntitlementsResponse(
    string ProductCode,
    Guid TenantId,
    string Status,
    string? PlanCode,
    string? PlanName,
    DateTimeOffset? CurrentPeriodEnd,
    bool HasAccess);

public record CreateCheckoutRequest
{
    [Url]
    public string? SuccessUrl { get; init; }

    [Url]
    public string? CancelUrl { get; init; }
}

public record CreatePortalRequest
{
    [Url]
    public string? ReturnUrl { get; init; }
}

public record BillingSessionResponse(string Url);
