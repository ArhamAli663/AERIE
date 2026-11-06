namespace AERIE.Api.Controllers;

using AERIE.Application.Auth.DTOs;
using AERIE.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterTenantRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterTenantAsync(request, ct);
        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (!result.IsSuccess)
            return Unauthorized(result);

        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentProfile(CancellationToken ct)
    {
        var result = await _authService.GetCurrentUserProfileAsync(ct);
        if (!result.IsSuccess)
            return NotFound(result);

        return Ok(result);
    }
}
