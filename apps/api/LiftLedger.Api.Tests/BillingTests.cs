using System.Net;
using System.Net.Http.Json;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LiftLedger.Api.Tests;

public class BillingStubTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public BillingStubTests(TestAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Entitlements_in_stub_mode_are_active()
    {
        var login = await _factory.CreateClient().LoginAsync("owner@humberhire.demo");
        var client = _factory.CreateClient().As(login.Token);

        var entitlements = await (await client.GetAsync("/api/billing/entitlements"))
            .ReadAsync<BillingEntitlementsResponse>();

        Assert.Equal("LiftLedger", entitlements.ProductCode);
        Assert.Equal(login.Tenant.Id, entitlements.TenantId);
        Assert.Equal("active", entitlements.Status);
        Assert.True(entitlements.HasAccess);
        Assert.Equal("pro", entitlements.PlanCode);
        Assert.Equal("Pro", entitlements.PlanTier);
        Assert.True(entitlements.IsPro);
        Assert.True(entitlements.CanUseCertificates);
    }

    [Fact]
    public async Task Owner_can_open_stub_checkout_and_portal()
    {
        var login = await _factory.CreateClient().LoginAsync("owner@humberhire.demo");
        var client = _factory.CreateClient().As(login.Token);

        var checkout = await (await client.PostAsJsonAsync("/api/billing/checkout", new { }))
            .ReadAsync<BillingSessionResponse>();
        Assert.StartsWith("https://billing.stub.local/checkout/", checkout.Url);
        Assert.Contains(login.Tenant.Id.ToString(), checkout.Url);

        var portal = await (await client.PostAsJsonAsync("/api/billing/portal", new { }))
            .ReadAsync<BillingSessionResponse>();
        Assert.StartsWith("https://billing.stub.local/portal/", portal.Url);
    }

    [Fact]
    public async Task Examiner_cannot_create_checkout_or_portal()
    {
        var login = await _factory.CreateClient().LoginAsync("examiner@humberhire.demo");
        var client = _factory.CreateClient().As(login.Token);

        var checkout = await client.PostAsJsonAsync("/api/billing/checkout", new { });
        var portal = await client.PostAsJsonAsync("/api/billing/portal", new { });

        Assert.Equal(HttpStatusCode.Forbidden, checkout.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, portal.StatusCode);
    }

    [Fact]
    public async Task Register_succeeds_while_subscription_stub_is_enabled()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            organisationName = "Billing Workshop Ltd",
            email = $"owner-{Guid.NewGuid():N}@billing.demo",
            fullName = "Billie Owner",
            password = "SecurePass123!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

public class InactiveSubscriptionTests : IClassFixture<InactiveSubscriptionFactory>
{
    private readonly InactiveSubscriptionFactory _factory;

    public InactiveSubscriptionTests(InactiveSubscriptionFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Authenticated_business_endpoints_return_402_when_subscription_is_inactive()
    {
        var login = await _factory.CreateClient().LoginAsync("owner@humberhire.demo");
        var client = _factory.CreateClient().As(login.Token);

        var dashboard = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.PaymentRequired, dashboard.StatusCode);

        var json = await dashboard.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(402, json.GetProperty("status").GetInt32());
        Assert.Equal("/api/billing/checkout", json.GetProperty("checkout").GetString());
        Assert.Equal("canceled", json.GetProperty("subscriptionStatus").GetString());
    }

    [Fact]
    public async Task Billing_and_auth_remain_reachable_when_subscription_is_inactive()
    {
        var login = await _factory.CreateClient().LoginAsync("owner@humberhire.demo");
        var client = _factory.CreateClient().As(login.Token);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var entitlements = await (await client.GetAsync("/api/billing/entitlements"))
            .ReadAsync<BillingEntitlementsResponse>();
        Assert.Equal("canceled", entitlements.Status);
        Assert.False(entitlements.HasAccess);

        var checkout = await client.PostAsJsonAsync("/api/billing/checkout", new { });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
    }

    [Fact]
    public async Task Register_upserts_tenant_to_subscription_api()
    {
        var client = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@wolds.demo";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            organisationName = "Wolds Billing Ltd",
            email,
            fullName = "Casey Wold",
            password = "SecurePass123!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.ReadAsync<AuthResponse>();

        var fake = _factory.Services.GetRequiredService<ISubscriptionClient>() as FakeInactiveSubscriptionClient;
        Assert.NotNull(fake);
        Assert.Contains(fake.Upserts, u =>
            u.ExternalTenantId == auth.Tenant.Id.ToString()
            && u.Name == "Wolds Billing Ltd"
            && u.OwnerEmail == email
            && u.ProductCode == "LiftLedger");
    }
}

public class InactiveSubscriptionFactory : TestAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("SubscriptionApi:UseStub", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISubscriptionClient>();
            services.AddSingleton<FakeInactiveSubscriptionClient>();
            services.AddSingleton<ISubscriptionClient>(sp =>
                sp.GetRequiredService<FakeInactiveSubscriptionClient>());
        });
    }
}

public sealed class FakeInactiveSubscriptionClient : ISubscriptionClient
{
    public List<UpsertTenantRequest> Upserts { get; } = [];

    public Task<EntitlementsDto> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new EntitlementsDto
        {
            ProductCode = "LiftLedger",
            TenantId = tenantId.ToString(),
            Status = "canceled",
            PlanCode = "pro",
            PlanName = "Pro"
        });
    }

    public Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken)
    {
        Upserts.Add(request);
        return Task.CompletedTask;
    }

    public Task<BillingSessionDto> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new BillingSessionDto { Url = "https://qck.example/checkout" });

    public Task<BillingSessionDto> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new BillingSessionDto { Url = "https://qck.example/portal" });
}

public class SubscriptionClientHttpTests
{
    [Fact]
    public async Task GetEntitlements_uses_product_and_tenant_path_with_api_key_header()
    {
        var handler = new RecordingHandler(_ =>
            JsonResponse("""{"status":"trialing","planCode":"workshop","planName":"Workshop"}"""));
        var client = CreateClient(handler);

        var entitlements = await client.GetEntitlementsAsync(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            CancellationToken.None);

        Assert.Equal("trialing", entitlements.Status);
        Assert.True(entitlements.HasAccess);
        Assert.Equal("workshop", entitlements.PlanCode);
        Assert.Equal("Workshop", entitlements.PlanName);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "http://qck.test/api/v1/entitlements/LiftLedger/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            request.RequestUri?.ToString());
        Assert.True(request.Headers.TryGetValues("X-Api-Key", out var keys));
        Assert.Equal("test-key", Assert.Single(keys));
    }

    [Fact]
    public async Task Upsert_and_sessions_post_to_qck_paths()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                return JsonResponse("{}", HttpStatusCode.OK);
            }

            return JsonResponse("""{"url":"https://hosted.example/session"}""", HttpStatusCode.Created);
        });
        var client = CreateClient(handler);
        var tenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        await client.UpsertTenantAsync(
            new UpsertTenantRequest
            {
                ProductCode = "LiftLedger",
                ExternalTenantId = tenantId.ToString(),
                Name = "Humber Hire",
                OwnerEmail = "owner@humberhire.demo"
            },
            CancellationToken.None);

        var checkout = await client.CreateCheckoutSessionAsync(
            new CreateCheckoutSessionRequest
            {
                ProductCode = "LiftLedger",
                ExternalTenantId = tenantId.ToString(),
                CustomerEmail = "owner@humberhire.demo"
            },
            CancellationToken.None);

        var portal = await client.CreatePortalSessionAsync(
            new CreatePortalSessionRequest
            {
                ProductCode = "LiftLedger",
                ExternalTenantId = tenantId.ToString()
            },
            CancellationToken.None);

        Assert.Equal("https://hosted.example/session", checkout.Url);
        Assert.Equal("https://hosted.example/session", portal.Url);
        Assert.Equal("http://qck.test/api/v1/tenants", handler.Requests[0].RequestUri?.ToString());
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal("http://qck.test/api/v1/checkout/sessions", handler.Requests[1].RequestUri?.ToString());
        Assert.Equal("http://qck.test/api/v1/portal/sessions", handler.Requests[2].RequestUri?.ToString());
    }

    private static SubscriptionClient CreateClient(RecordingHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://qck.test/") };
        var options = Microsoft.Extensions.Options.Options.Create(new SubscriptionApiOptions
        {
            BaseUrl = "http://qck.test/",
            ApiKey = "test-key",
            ProductCode = "LiftLedger",
            UseStub = false
        });
        return new SubscriptionClient(http, options, new SilentLogger());
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class SilentLogger : Microsoft.Extensions.Logging.ILogger<SubscriptionClient>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
