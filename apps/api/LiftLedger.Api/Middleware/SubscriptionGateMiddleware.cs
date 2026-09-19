using System.Net;
using System.Text.Json;
using LiftLedger.Api.Auth;
using LiftLedger.Api.Billing;
using Microsoft.AspNetCore.Authorization;

namespace LiftLedger.Api.Middleware;

public sealed class SubscriptionGateMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<SubscriptionGateMiddleware> _logger;

    public SubscriptionGateMiddleware(RequestDelegate next, ILogger<SubscriptionGateMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISubscriptionClient subscriptions, ICurrentUser currentUser)
    {
        if (ShouldSkip(context) || !currentUser.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        EntitlementsDto entitlements;
        try
        {
            entitlements = await subscriptions.GetEntitlementsAsync(currentUser.TenantId, context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to read Qck entitlements for tenant {TenantId}", currentUser.TenantId);
            await WritePaymentRequiredAsync(
                context,
                "LiftLedger could not verify this organisation’s subscription. Open billing to continue.",
                "unavailable");
            return;
        }

        if (entitlements.HasAccess)
        {
            context.Items["SubscriptionEntitlements"] = entitlements;
            await _next(context);
            return;
        }

        await WritePaymentRequiredAsync(
            context,
            "An active or trial LiftLedger subscription is required for this organisation.",
            entitlements.Status);
    }

    private static bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Equals("/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/billing", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            return false;
        }

        if (endpoint.Metadata.GetMetadata<SkipSubscriptionCheckAttribute>() is not null)
        {
            return true;
        }

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return true;
        }

        return false;
    }

    private static async Task WritePaymentRequiredAsync(HttpContext context, string detail, string subscriptionStatus)
    {
        context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired;
        context.Response.ContentType = "application/json";
        var payload = new
        {
            type = "https://httpstatuses.com/402",
            title = "Subscription required",
            status = 402,
            detail,
            checkout = "/api/billing/checkout",
            subscriptionStatus
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
