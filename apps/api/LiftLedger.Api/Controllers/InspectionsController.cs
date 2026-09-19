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
    private readonly DefectService _defects;

    public InspectionsController(InspectionService inspections, CertificateService certificates, DefectService defects)
    {
        _inspections = inspections;
        _certificates = certificates;
        _defects = defects;
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

    [HttpPost("{id:guid}/defects")]
    public async Task<ActionResult<DefectDto>> RaiseDefect(
        Guid id,
        RaiseDefectRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _defects.RaiseFromInspectionAsync(id, request, cancellationToken);
        return Created($"/api/defects/{created.Id}", created);
    }

    [HttpGet("{id:guid}/certificate")]
    public async Task<IActionResult> CertificateHtml(Guid id, CancellationToken cancellationToken)
    {
        var document = await _certificates.GetOrIssueDocumentAsync(id, cancellationToken);
        return Content(document.Html, "text/html; charset=utf-8");
    }

    [HttpGet("{id:guid}/certificate.pdf")]
    public async Task<IActionResult> CertificatePdf(Guid id, CancellationToken cancellationToken)
    {
        var document = await _certificates.GetOrIssueDocumentAsync(id, cancellationToken);
        return File(document.Pdf, "application/pdf", document.FileName);
    }
}
