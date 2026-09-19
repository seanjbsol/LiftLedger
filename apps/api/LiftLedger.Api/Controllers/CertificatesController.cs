using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiftLedger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/certificates")]
public class CertificatesController : ControllerBase
{
    private readonly CertificateService _certificates;

    public CertificatesController(CertificateService certificates)
    {
        _certificates = certificates;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CertificateSummary>>> List(
        [FromQuery] Guid? assetId,
        CancellationToken cancellationToken)
    {
        return Ok(await _certificates.ListAsync(assetId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CertificateSummary>> Get(Guid id, CancellationToken cancellationToken)
    {
        var certificate = await _certificates.GetStoredAsync(id, cancellationToken);
        return Ok(CertificateService.MapSummary(certificate));
    }

    [HttpGet("{id:guid}/html")]
    public async Task<IActionResult> Html(Guid id, CancellationToken cancellationToken)
    {
        var certificate = await _certificates.GetStoredAsync(id, cancellationToken);
        return Content(certificate.Html, "text/html; charset=utf-8");
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var certificate = await _certificates.GetStoredAsync(id, cancellationToken);
        return File(certificate.Pdf, "application/pdf", $"{certificate.CertificateNumber}.pdf");
    }
}

[ApiController]
[Authorize]
[Route("api/puwer")]
public class PuwerController : ControllerBase
{
    private readonly IFeatureGate _features;

    public PuwerController(IFeatureGate features)
    {
        _features = features;
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<PuwerTemplateDto>>> Templates(CancellationToken cancellationToken)
    {
        await _features.EnsureCertificatesAsync(cancellationToken);
        return Ok(PuwerTemplates.All);
    }

    [HttpGet("templates/{category}")]
    public async Task<ActionResult<PuwerTemplateDto>> Template(string category, CancellationToken cancellationToken)
    {
        await _features.EnsureCertificatesAsync(cancellationToken);
        if (!Enum.TryParse<Domain.AssetCategory>(category, true, out var parsed))
        {
            return NotFound();
        }

        return Ok(PuwerTemplates.ForCategory(parsed));
    }
}

[ApiController]
[Authorize]
[Route("api/portal")]
public class PortalController : ControllerBase
{
    private readonly ClientPortalService _portal;

    public PortalController(ClientPortalService portal)
    {
        _portal = portal;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PublicPortalCertificate>>> Index(CancellationToken cancellationToken)
    {
        return Ok(await _portal.ListForCurrentTenantAsync(cancellationToken));
    }

    [HttpGet("tokens")]
    public async Task<ActionResult<IReadOnlyList<PortalTokenResponse>>> Tokens(CancellationToken cancellationToken)
    {
        return Ok(await _portal.ListTokensAsync(cancellationToken));
    }

    [HttpPost("tokens")]
    public async Task<ActionResult<PortalTokenResponse>> CreateToken(
        CreatePortalTokenRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _portal.CreateTokenAsync(request, cancellationToken);
        return Created(created.PublicUrl, created);
    }
}

[ApiController]
[AllowAnonymous]
[SkipSubscriptionCheck]
[Route("api/public/portal")]
public class PublicPortalController : ControllerBase
{
    private readonly ClientPortalService _portal;

    public PublicPortalController(ClientPortalService portal)
    {
        _portal = portal;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<PublicPortalView>> Get(string token, CancellationToken cancellationToken)
    {
        return Ok(await _portal.GetPublicAsync(token, cancellationToken));
    }

    [HttpGet("{token}/certificates/{id:guid}")]
    public async Task<IActionResult> CertificateHtml(string token, Guid id, CancellationToken cancellationToken)
    {
        var certificate = await _portal.GetPublicCertificateAsync(token, id, cancellationToken);
        return Content(certificate.Html, "text/html; charset=utf-8");
    }

    [HttpGet("{token}/certificates/{id:guid}.pdf")]
    public async Task<IActionResult> CertificatePdf(string token, Guid id, CancellationToken cancellationToken)
    {
        var certificate = await _portal.GetPublicCertificateAsync(token, id, cancellationToken);
        return File(certificate.Pdf, "application/pdf", $"{certificate.CertificateNumber}.pdf");
    }
}
