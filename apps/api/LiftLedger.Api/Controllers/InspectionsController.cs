using LiftLedger.Api.Contracts;
using LiftLedger.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiftLedger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inspections")]
public class InspectionsController : ControllerBase
{
    private readonly InspectionService _inspections;
    private readonly CertificateService _certificates;

    public InspectionsController(InspectionService inspections, CertificateService certificates)
    {
        _inspections = inspections;
        _certificates = certificates;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InspectionSummary>>> List(
        [FromQuery] Guid? assetId,
        CancellationToken cancellationToken)
    {
        return Ok(await _inspections.ListAsync(assetId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InspectionDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _inspections.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<InspectionDetail>> Start(StartInspectionRequest request, CancellationToken cancellationToken)
    {
        var created = await _inspections.StartAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<InspectionDetail>> Complete(
        Guid id,
        CompleteInspectionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _inspections.CompleteAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/certificate")]
    public async Task<IActionResult> CertificateHtml(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _certificates.LoadCompletedAsync(id, cancellationToken);
        return Content(_certificates.BuildHtml(inspection), "text/html; charset=utf-8");
    }

    [HttpGet("{id:guid}/certificate.pdf")]
    public async Task<IActionResult> CertificatePdf(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _certificates.LoadCompletedAsync(id, cancellationToken);
        var pdf = _certificates.BuildPdf(inspection);
        var name = $"{inspection.CertificateNumber ?? inspection.Id.ToString("N")}.pdf";
        return File(pdf, "application/pdf", name);
    }
}
