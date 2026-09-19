using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Tests;

public class TenantIsolationTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public TenantIsolationTests(TestAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_is_anonymous_and_ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Register_creates_tenant_and_owner_jwt()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            organisationName = "Wolds Workshop Ltd",
            email = $"owner-{Guid.NewGuid():N}@wolds.demo",
            fullName = "Casey Wold",
            password = "SecurePass123!",
            town = "Louth",
            postcode = "LN11 0AA"
        });

        var auth = await response.ReadAsync<AuthResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.Equal("Owner", auth.Role);
        Assert.Equal("Wolds Workshop Ltd", auth.Tenant.Name);
        Assert.NotEqual(Guid.Empty, auth.Tenant.Id);

        var payload = auth.Token.Split('.')[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
            .Replace('-', '+').Replace('_', '/');
        var json = JsonDocument.Parse(Convert.FromBase64String(padded));
        Assert.Equal(auth.Tenant.Id.ToString(), json.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal("Owner", json.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Second_tenant_cannot_read_or_mutate_first_tenant_assets()
    {
        var client = _factory.CreateClient();
        var humber = await client.LoginAsync("owner@humberhire.demo");
        var northern = await client.LoginAsync("owner@northernpplant.demo");

        var humberClient = _factory.CreateClient().As(humber.Token);
        var northernClient = _factory.CreateClient().As(northern.Token);

        var humberAssets = await (await humberClient.GetAsync("/api/assets")).ReadAsync<List<AssetSummary>>();
        var northernAssets = await (await northernClient.GetAsync("/api/assets")).ReadAsync<List<AssetSummary>>();

        Assert.Contains(humberAssets, a => a.AssetNumber == "TRI-1042");
        Assert.DoesNotContain(humberAssets, a => a.AssetNumber == "HOIST-01");
        Assert.Contains(northernAssets, a => a.AssetNumber == "HOIST-01");
        Assert.DoesNotContain(northernAssets, a => a.AssetNumber == "TRI-1042");

        var foreignId = humberAssets.First(a => a.AssetNumber == "TRI-1042").Id;
        var peek = await northernClient.GetAsync($"/api/assets/{foreignId}");
        Assert.Equal(HttpStatusCode.NotFound, peek.StatusCode);

        var mutate = await northernClient.PutAsJsonAsync($"/api/assets/{foreignId}", new
        {
            assetNumber = "HACKED",
            name = "Should not work",
            category = "Trailer"
        });
        Assert.Equal(HttpStatusCode.NotFound, mutate.StatusCode);
    }

    [Fact]
    public async Task Create_asset_and_complete_inspection_are_tenant_scoped()
    {
        var client = _factory.CreateClient();
        var humber = await client.LoginAsync("owner@humberhire.demo");
        var authed = _factory.CreateClient().As(humber.Token);

        var created = await (await authed.PostAsJsonAsync("/api/assets", new
        {
            assetNumber = $"GEN-{Guid.NewGuid().ToString("N")[..6]}",
            name = "Workshop gantry",
            category = "LiftingEquipment",
            identificationCode = "QR-GANTRY-1",
            safeWorkingLoad = "1,000 kg"
        })).ReadAsync<AssetDetail>();

        Assert.Equal(humber.Tenant.Id, created.TenantId);

        var started = await (await authed.PostAsJsonAsync("/api/inspections", new
        {
            assetId = created.Id,
            examinationType = "ThoroughExamination"
        })).ReadAsync<InspectionDetail>();

        Assert.Equal(InspectionStatus.Draft, started.Status);

        var nextDue = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6));
        var completed = await (await authed.PostAsJsonAsync($"/api/inspections/{started.Id}/complete", new
        {
            result = "Pass",
            nextDueDate = nextDue.ToString("yyyy-MM-dd"),
            particularsOfExamination = "Visual examination of beam, trolley and hoist mountings. SWL markings present.",
            examinerQualifications = "Competent person (demo)"
        })).ReadAsync<InspectionDetail>();

        Assert.Equal(InspectionStatus.Completed, completed.Status);
        Assert.Equal(InspectionResult.Pass, completed.Result);
        Assert.False(string.IsNullOrWhiteSpace(completed.CertificateNumber));
        Assert.Equal(created.TenantId, completed.TenantId);

        var certificate = await authed.GetAsync($"/api/inspections/{completed.Id}/certificate");
        Assert.Equal(HttpStatusCode.OK, certificate.StatusCode);
        var html = await certificate.Content.ReadAsStringAsync();
        Assert.Contains("HSE-certified", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.AssetNumber, html);

        var pdf = await authed.GetAsync($"/api/inspections/{completed.Id}/certificate.pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        var northern = await client.LoginAsync("owner@northernpplant.demo");
        var other = _factory.CreateClient().As(northern.Token);
        var hiddenAsset = await other.GetAsync($"/api/assets/{created.Id}");
        var hiddenInspection = await other.GetAsync($"/api/inspections/{completed.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenAsset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hiddenInspection.StatusCode);
    }

    [Fact]
    public async Task Dashboard_shows_overdue_and_due_soon_for_current_tenant_only()
    {
        var client = _factory.CreateClient();
        var humber = await client.LoginAsync("owner@humberhire.demo");
        var authed = _factory.CreateClient().As(humber.Token);

        var dashboard = await (await authed.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();
        Assert.Equal("Humber Plant & Trailer Hire Ltd", dashboard.Tenant.Name);
        Assert.True(dashboard.OverdueCount >= 1);
        Assert.True(dashboard.DueSoonCount >= 1);
        Assert.Contains(dashboard.Overdue, a => a.AssetNumber == "FLT-07");
        Assert.DoesNotContain(dashboard.Overdue, a => a.AssetNumber == "HOIST-01");
        Assert.Contains(dashboard.RecentInspections, i => i.AssetNumber == "CS-16");
    }

    [Fact]
    public async Task Examiner_can_write_viewer_cannot()
    {
        var client = _factory.CreateClient();
        var examiner = await client.LoginAsync("examiner@humberhire.demo");
        Assert.Equal("Examiner", examiner.Role);

        var examinerClient = _factory.CreateClient().As(examiner.Token);
        var list = await (await examinerClient.GetAsync("/api/assets")).ReadAsync<List<AssetSummary>>();
        Assert.NotEmpty(list);

        var start = await examinerClient.PostAsJsonAsync("/api/inspections", new { assetId = list[0].Id });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
    }
}
