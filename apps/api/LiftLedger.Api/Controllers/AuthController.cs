using LiftLedger.Api.Contracts;
using LiftLedger.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiftLedger.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Creates a new workshop/hire organisation and an owner account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.RegisterAsync(request, cancellationToken);
        return Created("/api/auth/me", result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _auth.LoginAsync(request, cancellationToken));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        return Ok(await _auth.GetMeAsync(cancellationToken));
    }

    [HttpGet("members")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> Members(CancellationToken cancellationToken)
    {
        return Ok(await _auth.ListMembersAsync(cancellationToken));
    }
}
