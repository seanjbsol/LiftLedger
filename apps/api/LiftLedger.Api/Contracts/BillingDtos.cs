using System.ComponentModel.DataAnnotations;

namespace LiftLedger.Api.Contracts;

public record BillingEntitlementsResponse(
    string ProductCode,
    Guid TenantId,
    string Status,
    string? PlanCode,
    string? PlanName,
    DateTimeOffset? CurrentPeriodEnd,
    bool HasAccess,
    string PlanTier = "Starter",
    bool IsPro = false,
    bool CanUseCertificates = false,
    bool CanUseDefects = false,
    bool CanUseClientPortal = false);

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
