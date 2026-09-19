using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LiftLedger.Api.Tests;

public class CertificateAndDefectTests : IClassFixture<TestAppFactory>
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly TestAppFactory _factory;

    public CertificateAndDefectTests(TestAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Completing_loler_exam_stores_schedule1_certificate()
    {
        var authed = await OwnerClientAsync();
        var asset = await CreateAssetAsync(authed, "LiftingEquipment", "QR-GANTRY-CERT");

        var started = await (await authed.PostAsJsonAsync("/api/inspections", new
        {
            assetId = asset.Id,
            examinationType = "ThoroughExamination"
        })).ReadAsync<InspectionDetail>();

        var nextDue = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6));
        var completed = await (await authed.PostAsJsonAsync($"/api/inspections/{started.Id}/complete", new
        {
            result = "Pass",
            nextDueDate = nextDue.ToString("yyyy-MM-dd"),
            dateOfLastExamination = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)).ToString("yyyy-MM-dd"),
            safeWorkingLoad = "1,000 kg",
            employerName = "Humber Plant & Trailer Hire Ltd",
            employerAddress = "Westgate Works, Grimsby, DN31 2TG",
            premisesAddress = "Westgate Works, Grimsby",
            particularsOfExamination = "Visual examination of beam, trolley and hoist mountings. SWL markings present.",
            particularsOfTests = "No load test required at this examination.",
            examinerQualifications = "Competent person (demo)",
            authenticatingPerson = "Alex Houghton",
            declaration = "Equipment may remain in service until the next thorough examination."
        })).ReadAsync<InspectionDetail>();

        Assert.Equal(InspectionStatus.Completed, completed.Status);
        Assert.NotNull(completed.StoredCertificateId);
        Assert.False(string.IsNullOrWhiteSpace(completed.CertificateNumber));
        Assert.Equal("1,000 kg", completed.SafeWorkingLoad);
        Assert.Equal("Humber Plant & Trailer Hire Ltd", completed.EmployerName);

        var stored = await (await authed.GetAsync($"/api/certificates/{completed.StoredCertificateId}"))
            .ReadAsync<CertificateSummary>();
        Assert.Equal(CertificateKind.LolerThoroughExamination, stored.Kind);
        Assert.Equal("loler-schedule1-v1", stored.TemplateCode);
        Assert.Equal(completed.CertificateNumber, stored.CertificateNumber);

        var html = await (await authed.GetAsync($"/api/certificates/{stored.Id}/html")).Content.ReadAsStringAsync();
        Assert.Contains("HSE-certified", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Schedule 1", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(asset.AssetNumber, html);
        Assert.Contains("1,000 kg", html);
        Assert.Contains("Westgate Works", html);
        Assert.Contains("Particulars of any test", html);

        var pdf = await authed.GetAsync($"/api/certificates/{stored.Id}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        var listed = await (await authed.GetAsync("/api/certificates")).ReadAsync<List<CertificateSummary>>();
        Assert.Contains(listed, c => c.Id == stored.Id);
    }

    [Fact]
    public async Task Completing_puwer_assessment_stores_asset_class_template()
    {
        var authed = await OwnerClientAsync();
        var asset = await CreateAssetAsync(authed, "Plant", "QR-PUWER-PLANT");

        var template = await (await authed.GetAsync("/api/puwer/templates/Plant")).ReadAsync<PuwerTemplateDto>();
        Assert.Equal("puwer-plant-v1", template.Code);
        Assert.Contains(template.Items, i => i.Id == "guards");

        var started = await (await authed.PostAsJsonAsync("/api/inspections", new
        {
            assetId = asset.Id,
            examinationType = "PuwerInspection"
        })).ReadAsync<InspectionDetail>();

        Assert.Equal("puwer-plant-v1", started.PuwerTemplateCode);

        var nextDue = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        var completed = await (await authed.PostAsJsonAsync($"/api/inspections/{started.Id}/complete", new
        {
            result = "Pass",
            nextDueDate = nextDue.ToString("yyyy-MM-dd"),
            puwerTemplateCode = "puwer-plant-v1",
            particularsOfExamination = "Plant is suitable for yard use with existing guards in place.",
            puwerAnswers = template.Items.Select(item => new
            {
                itemId = item.Id,
                result = "Suitable",
                notes = "Checked"
            })
        })).ReadAsync<InspectionDetail>();

        Assert.NotNull(completed.StoredCertificateId);
        Assert.StartsWith("PW-", completed.CertificateNumber);
        Assert.Equal(ExaminationType.PuwerInspection, completed.ExaminationType);

        var html = await (await authed.GetAsync($"/api/inspections/{completed.Id}/certificate")).Content.ReadAsStringAsync();
        Assert.Contains("PUWER assessment", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not an HSE-certified", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Guards", html);
    }

    [Fact]
    public async Task Defect_transitions_assign_photos_close_and_retest()
    {
        var authed = await OwnerClientAsync();
        var members = await (await authed.GetAsync("/api/auth/members")).ReadAsync<List<MemberDto>>();
        var examiner = members.First(m => m.Role == MembershipRole.Examiner);

        var asset = await CreateAssetAsync(authed, "Plant", "QR-DEFECT-FLOW");
        var started = await (await authed.PostAsJsonAsync("/api/inspections", new
        {
            assetId = asset.Id,
            examinationType = "ThoroughExamination"
        })).ReadAsync<InspectionDetail>();

        var nextDue = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6));
        var completed = await (await authed.PostAsJsonAsync($"/api/inspections/{started.Id}/complete", new
        {
            result = "PassWithDefects",
            nextDueDate = nextDue.ToString("yyyy-MM-dd"),
            particularsOfExamination = "Forks and mast examined.",
            particularsOfDefects = "Heel wear on both forks.",
            defects = new[]
            {
                new
                {
                    description = "Fork heel wear approaching rejection limit.",
                    severity = "Defect",
                    category = "Forks",
                    requiresImmediateWithdrawal = false
                }
            }
        })).ReadAsync<InspectionDetail>();

        var raised = Assert.Single(completed.Defects);
        Assert.Equal(DefectStatus.Open, raised.Status);

        var extra = await (await authed.PostAsJsonAsync($"/api/inspections/{completed.Id}/defects", new
        {
            description = "Hydraulic weep at carriage ram.",
            severity = "Defect",
            category = "Hydraulics"
        })).ReadAsync<DefectDto>();
        Assert.Equal(DefectStatus.Open, extra.Status);

        var assigned = await (await authed.PostAsJsonAsync($"/api/defects/{raised.Id}/assign", new
        {
            assignedToUserId = examiner.UserId
        })).ReadAsync<DefectDto>();
        Assert.Equal(DefectStatus.Assigned, assigned.Status);
        Assert.Equal(examiner.UserId, assigned.AssignedToUserId);
        Assert.Equal("Jordan Blake", assigned.AssignedToName);

        await UploadPhotoAsync(authed, raised.Id, "Before", "before.png");
        var closeWithoutAfter = await authed.PostAsJsonAsync($"/api/defects/{raised.Id}/close", new { closeNotes = "Too soon" });
        Assert.Equal(HttpStatusCode.BadRequest, closeWithoutAfter.StatusCode);

        await UploadPhotoAsync(authed, raised.Id, "After", "after.png");
        var closed = await (await authed.PostAsJsonAsync($"/api/defects/{raised.Id}/close", new
        {
            closeNotes = "Forks replaced. Plant returned to hire fleet."
        })).ReadAsync<DefectDto>();
        Assert.Equal(DefectStatus.Closed, closed.Status);
        Assert.NotNull(closed.RectifiedAt);
        Assert.Equal(2, closed.Photos.Count);

        var photo = await authed.GetAsync($"/api/defects/{raised.Id}/photos/{closed.Photos[0].Id}");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);

        await UploadPhotoAsync(authed, extra.Id, "Before", "before-2.png");
        var retestRequired = await (await authed.PostAsJsonAsync($"/api/defects/{extra.Id}/close", new
        {
            closeNotes = "Ram resealed. Thorough examination required before return to service.",
            requiresRetest = true
        })).ReadAsync<DefectDto>();
        Assert.Equal(DefectStatus.RetestRequired, retestRequired.Status);
        Assert.True(retestRequired.RequiresRetest);
        Assert.Null(retestRequired.RectifiedAt);

        var retest = await (await authed.PostAsJsonAsync($"/api/defects/{extra.Id}/retest", new { }))
            .ReadAsync<InspectionDetail>();
        Assert.Equal(asset.Id, retest.AssetId);
        Assert.Equal(InspectionStatus.Draft, retest.Status);

        var linked = await (await authed.GetAsync($"/api/defects/{extra.Id}")).ReadAsync<DefectDto>();
        Assert.Equal(retest.Id, linked.RetestInspectionId);

        var northern = await _factory.CreateClient().LoginAsync("owner@northernpplant.demo");
        var other = _factory.CreateClient().As(northern.Token);
        var hidden = await other.GetAsync($"/api/defects/{raised.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Asset_qr_code_resolves_to_last_certificate()
    {
        var authed = await OwnerClientAsync();
        var code = $"QR-SCAN-{Guid.NewGuid():N}"[..18];
        var asset = await CreateAssetAsync(authed, "Trailer", code);

        var started = await (await authed.PostAsJsonAsync("/api/inspections", new { assetId = asset.Id }))
            .ReadAsync<InspectionDetail>();
        var completed = await (await authed.PostAsJsonAsync($"/api/inspections/{started.Id}/complete", new
        {
            result = "Pass",
            nextDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)).ToString("yyyy-MM-dd"),
            particularsOfExamination = "Trailer structure, coupling and lights examined."
        })).ReadAsync<InspectionDetail>();

        var scan = await (await authed.GetAsync($"/api/assets/by-code/{code}")).ReadAsync<AssetScanResponse>();
        Assert.Equal(asset.Id, scan.Id);
        Assert.Equal(completed.Id, scan.LastInspectionId);
        Assert.Equal(completed.StoredCertificateId, scan.LastCertificateId);
        Assert.StartsWith("liftledger://a/", scan.DeepLink);

        var qr = await authed.GetAsync($"/api/assets/{asset.Id}/qr");
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal("image/png", qr.Content.Headers.ContentType?.MediaType);
        var bytes = await qr.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
    }

    [Fact]
    public async Task Client_portal_stub_shares_last_certificates_by_token()
    {
        var authed = await OwnerClientAsync();
        var clients = await (await authed.GetAsync("/api/clients")).ReadAsync<List<ClientDto>>();
        var client = Assert.Single(clients, c => c.Name.Contains("North Lincs"));

        var token = await (await authed.PostAsJsonAsync("/api/portal/tokens", new { clientId = client.Id }))
            .ReadAsync<PortalTokenResponse>();
        Assert.Contains("/api/public/portal/", token.PublicUrl);

        var publicClient = _factory.CreateClient();
        var view = await (await publicClient.GetAsync(token.PublicUrl)).ReadAsync<PublicPortalView>();
        Assert.Equal("North Lincs Civils Ltd", view.ClientName);
        Assert.Contains("not HSE-certified", view.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> OwnerClientAsync()
    {
        var login = await _factory.CreateClient().LoginAsync("owner@humberhire.demo");
        return _factory.CreateClient().As(login.Token);
    }

    private static async Task<AssetDetail> CreateAssetAsync(HttpClient authed, string category, string code)
    {
        return await (await authed.PostAsJsonAsync("/api/assets", new
        {
            assetNumber = $"T-{Guid.NewGuid().ToString("N")[..6]}",
            name = "Test asset",
            category,
            identificationCode = code,
            safeWorkingLoad = "2,000 kg"
        })).ReadAsync<AssetDetail>();
    }

    private static async Task UploadPhotoAsync(HttpClient client, Guid defectId, string kind, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(kind), "kind");
        var file = new ByteArrayContent(TinyPng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", fileName);
        var response = await client.PostAsync($"/api/defects/{defectId}/photos", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

public class EntitlementGateTests : IClassFixture<StarterPlanFactory>
{
    private readonly StarterPlanFactory _factory;

    public EntitlementGateTests(StarterPlanFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Starter_can_use_asset_list_and_simple_exam_log()
    {
        var login = await _factory.CreateClient().LoginAsync("owner@humberhire.demo");
        var client = _factory.CreateClient().As(login.Token);

        var entitlements = await (await client.GetAsync("/api/billing/entitlements"))
            .ReadAsync<BillingEntitlementsResponse>();
        Assert.True(entitlements.HasAccess);
        Assert.Equal("Starter", entitlements.PlanTier);
        Assert.False(entitlements.IsPro);
        Assert.False(entitlements.CanUseCertificates);
        Assert.False(entitlements.CanUseDefects);
        Assert.False(entitlements.CanUseClientPortal);

        var assets = await (await client.GetAsync("/api/assets")).ReadAsync<List<AssetSummary>>();
        Assert.Contains(assets, a => a.AssetNumber == "TRI-1042");

        var created = await (await client.PostAsJsonAsync("/api/assets", new
        {
            assetNumber = $"ST-{Guid.NewGuid().ToString("N")[..6]}",
            name = "Starter log asset",
            category = "Trailer"
        })).ReadAsync<AssetDetail>();

        var started = await (await client.PostAsJsonAsync("/api/inspections", new { assetId = created.Id }))
            .ReadAsync<InspectionDetail>();
        var completed = await (await client.PostAsJsonAsync($"/api/inspections/{started.Id}/complete", new
        {
            result = "Pass",
            nextDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)).ToString("yyyy-MM-dd"),
            particularsOfExamination = "Simple examination log entry."
        })).ReadAsync<InspectionDetail>();

        Assert.Equal(InspectionStatus.Completed, completed.Status);
        Assert.Null(completed.StoredCertificateId);
        Assert.False(string.IsNullOrWhiteSpace(completed.CertificateNumber));
    }

    [Fact]
    public async Task Starter_is_gated_on_certificates_defects_and_client_portal()
    {
        var login = await _factory.CreateClient().LoginAsync("owner@humberhire.demo");
        var client = _factory.CreateClient().As(login.Token);

        var seedInspection = await (await client.GetAsync("/api/inspections")).ReadAsync<List<InspectionSummary>>();
        var completed = seedInspection.First(i => i.Status == InspectionStatus.Completed);

        await AssertPaywall(client.GetAsync($"/api/inspections/{completed.Id}/certificate"), "certificates");
        await AssertPaywall(client.GetAsync("/api/certificates"), "certificates");
        await AssertPaywall(client.GetAsync("/api/puwer/templates"), "certificates");
        await AssertPaywall(client.GetAsync("/api/defects"), "defects");
        await AssertPaywall(client.PostAsJsonAsync($"/api/inspections/{completed.Id}/defects", new
        {
            description = "Should not raise on Starter."
        }), "defects");
        await AssertPaywall(client.GetAsync("/api/portal"), "clientPortal");
        await AssertPaywall(client.PostAsJsonAsync("/api/portal/tokens", new
        {
            clientId = Guid.Parse("33333333-3333-3333-3333-333333333331")
        }), "clientPortal");
    }

    private static async Task AssertPaywall(Task<HttpResponseMessage> call, string feature)
    {
        var response = await call;
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(402, json.GetProperty("status").GetInt32());
        Assert.Equal("/api/billing/checkout", json.GetProperty("checkout").GetString());
        Assert.Equal(feature, json.GetProperty("feature").GetString());
        Assert.Equal("Pro", json.GetProperty("requiredPlan").GetString());
    }
}

public class StarterPlanFactory : TestAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("SubscriptionApi:UseStub", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISubscriptionClient>();
            services.AddSingleton<ISubscriptionClient, FakeStarterSubscriptionClient>();
        });
    }
}

public sealed class FakeStarterSubscriptionClient : ISubscriptionClient
{
    public Task<EntitlementsDto> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(new EntitlementsDto
        {
            ProductCode = "LiftLedger",
            TenantId = tenantId.ToString(),
            Status = "active",
            PlanCode = "starter",
            PlanName = "Starter"
        });

    public Task UpsertTenantAsync(UpsertTenantRequest request, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<BillingSessionDto> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new BillingSessionDto { Url = "https://qck.example/checkout" });

    public Task<BillingSessionDto> CreatePortalSessionAsync(
        CreatePortalSessionRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new BillingSessionDto { Url = "https://qck.example/portal" });
}
