using LiftLedger.Api.Contracts;
using LiftLedger.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiftLedger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly ClientService _clients;

    public ClientsController(ClientService clients)
    {
        _clients = clients;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _clients.ListClientsAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(UpsertClientRequest request, CancellationToken cancellationToken)
    {
        var created = await _clients.CreateClientAsync(request, cancellationToken);
        return Created("/api/clients", created);
    }
}

[ApiController]
[Authorize]
[Route("api/sites")]
public class SitesController : ControllerBase
{
    private readonly ClientService _clients;

    public SitesController(ClientService clients)
    {
        _clients = clients;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SiteDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _clients.ListSitesAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<SiteDto>> Create(UpsertSiteRequest request, CancellationToken cancellationToken)
    {
        var created = await _clients.CreateSiteAsync(request, cancellationToken);
        return Created("/api/sites", created);
    }
}
