using LiftLedger.Api.Contracts;
using LiftLedger.Api.Domain;
using LiftLedger.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiftLedger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/defects")]
public class DefectsController : ControllerBase
{
    private readonly DefectService _defects;

    public DefectsController(DefectService defects)
    {
        _defects = defects;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DefectDto>>> List(
        [FromQuery] Guid? assetId,
        [FromQuery] DefectStatus? status,
        CancellationToken cancellationToken)
    {
        return Ok(await _defects.ListAsync(assetId, status, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DefectDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _defects.GetAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<DefectDto>> Assign(
        Guid id,
        AssignDefectRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _defects.AssignAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/photos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<DefectPhotoDto>> AddPhoto(
        Guid id,
        [FromForm] DefectPhotoKind kind,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ArgumentException("A photo file is required.");
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var photo = await _defects.AddPhotoAsync(
            id,
            kind,
            file.FileName,
            contentType,
            memory.ToArray(),
            cancellationToken);
        return Created($"/api/defects/{id}/photos/{photo.Id}", photo);
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid id, Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await _defects.GetPhotoAsync(id, photoId, cancellationToken);
        return File(photo.Data, photo.ContentType, photo.FileName);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<DefectDto>> Close(
        Guid id,
        CloseDefectRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _defects.CloseAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/retest")]
    public async Task<ActionResult<InspectionDetail>> Retest(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await _defects.StartRetestAsync(id, cancellationToken);
        return Created($"/api/inspections/{inspection.Id}", inspection);
    }
}
