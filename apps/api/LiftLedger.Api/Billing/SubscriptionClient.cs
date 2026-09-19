using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LiftLedger.Api.Billing;

/// <summary>
/// HttpClient for the central QckApp Subscription API. LiftLedger never talks to Stripe directly.
/// </summary>
public sealed class SubscriptionClient : ISubscriptionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly SubscriptionApiOptions _options;
    private readonly ILogger<SubscriptionClient> _logger;

    public SubscriptionClient(HttpClient http, IOptions<SubscriptionApiOptions> options, ILogger<SubscriptionClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EntitlementsDto> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var product = Uri.EscapeDataString(_options.ProductCode);
        var path = $"api/v1/entitlements/{product}/{tenantId:D}";
        using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new EntitlementsDto
            {
                ProductCode = _options.ProductCode,
                TenantId = tenantId.ToString(),
                Status = "none"
            };
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadFromJsonAsync<QckEntitlementsResponse>(JsonOptions, cancellationToken)
                      .ConfigureAwait(false);

        var status = FirstNonEmpty(payload?.Status, payload?.SubscriptionStatus) ?? "none";
        return new EntitlementsDto
        {
            ProductCode = FirstNonEmpty(payload?.ProductCode, _options.ProductCode) ?? _options.ProductCode,
            TenantId = FirstNonEmpty(payload?.TenantId, payload?.ExternalTenantId, tenantId.ToString()) ?? tenantId.ToString(),
            Status = status,
            PlanCode = payload?.PlanCode,
            PlanName = FirstNonEmpty(payload?.PlanName, payload?.Plan),
            CurrentPeriodEnd = payload?.CurrentPeriodEnd,
            CheckoutUrl = payload?.CheckoutUrl
        };
    }

    public async Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            productCode = string.IsNullOrWhiteSpace(request.ProductCode) ? _options.ProductCode : request.ProductCode,
            externalTenantId = request.ExternalTenantId,
            name = request.Name,
            ownerEmail = request.OwnerEmail
        };

        using var response = await SendAsync(HttpMethod.Put, "api/v1/tenants", body, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<BillingSessionDto> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken) =>
        CreateSessionAsync(
            "api/v1/checkout/sessions",
            new
            {
                productCode = string.IsNullOrWhiteSpace(request.ProductCode) ? _options.ProductCode : request.ProductCode,
                externalTenantId = request.ExternalTenantId,
                customerEmail = request.CustomerEmail,
                successUrl = request.SuccessUrl,
                cancelUrl = request.CancelUrl
            },
            cancellationToken);

    public Task<BillingSessionDto> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken) =>
        CreateSessionAsync(
            "api/v1/portal/sessions",
            new
            {
                productCode = string.IsNullOrWhiteSpace(request.ProductCode) ? _options.ProductCode : request.ProductCode,
                externalTenantId = request.ExternalTenantId,
                returnUrl = request.ReturnUrl
            },
            cancellationToken);

    private async Task<BillingSessionDto> CreateSessionAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, path, body, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadFromJsonAsync<QckSessionResponse>(JsonOptions, cancellationToken)
                      .ConfigureAwait(false);
        var url = FirstNonEmpty(payload?.Url, payload?.CheckoutUrl, payload?.PortalUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Qck subscription API did not return a session URL.");
        }

        return new BillingSessionDto { Url = url };
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new InvalidOperationException(
                "SubscriptionApi:BaseUrl is not configured. Set SubscriptionApi__BaseUrl or enable SubscriptionApi:UseStub.");
        }

        var request = new HttpRequestMessage(method, relativePath);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, _options.ApiKey);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogWarning(
            "Qck subscription API {Method} {Uri} returned {Status}: {Body}",
            response.RequestMessage?.Method,
            response.RequestMessage?.RequestUri,
            (int)response.StatusCode,
            body);

        throw new HttpRequestException(
            $"Qck subscription API returned {(int)response.StatusCode} {response.ReasonPhrase}.",
            inner: null,
            statusCode: response.StatusCode);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
