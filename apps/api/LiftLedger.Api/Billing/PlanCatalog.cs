namespace LiftLedger.Api.Billing;

public static class PlanFeatures
{
    public const string Certificates = "certificates";
    public const string Defects = "defects";
    public const string ClientPortal = "clientPortal";
}

public static class PlanTiers
{
    public const string Starter = "Starter";
    public const string Pro = "Pro";
}

/// <summary>
/// Maps Qck plan codes onto LiftLedger Starter vs Pro.
/// Starter: asset list + simple examination log.
/// Pro: stored certificate builders, defect workflow, client download portal.
/// </summary>
public static class PlanCatalog
{
    public static bool IsPro(string? planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            return false;
        }

        var code = planCode.Trim().ToLowerInvariant();
        if (code.Contains("starter", StringComparison.Ordinal)
            || code is "basic" or "free" or "lite")
        {
            return false;
        }

        return code is "pro" or "stub" or "professional" or "workshop"
               || code.Contains("pro", StringComparison.Ordinal);
    }

    public static string Tier(string? planCode) => IsPro(planCode) ? PlanTiers.Pro : PlanTiers.Starter;

    public static PlanSnapshot From(EntitlementsDto entitlements)
    {
        var isPro = entitlements.HasAccess && IsPro(entitlements.PlanCode);
        return new PlanSnapshot(
            entitlements.Status,
            entitlements.PlanCode,
            entitlements.PlanName,
            Tier(entitlements.PlanCode),
            entitlements.HasAccess,
            isPro,
            isPro,
            isPro,
            isPro);
    }
}

public sealed record PlanSnapshot(
    string Status,
    string? PlanCode,
    string? PlanName,
    string PlanTier,
    bool HasAccess,
    bool IsPro,
    bool CanUseCertificates,
    bool CanUseDefects,
    bool CanUseClientPortal);
