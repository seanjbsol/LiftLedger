using LiftLedger.Api.Contracts;
using LiftLedger.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiftLedger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assets")]
public class AssetsController : ControllerBase
{
    private readonly AssetService _assets;

    public AssetsController(AssetService assets)
    {
        _assets = assets;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssetSummary>>> List([FromQuery] string? q, CancellationToken cancellationToken)
    {
        return Ok(await _assets.ListAsync(q, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssetDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _assets.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AssetDetail>> Create(UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        var created = await _assets.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssetDetail>> Update(Guid id, UpsertAssetRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _assets.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await _assets.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }
}
