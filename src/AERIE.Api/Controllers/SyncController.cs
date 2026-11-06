namespace AERIE.Api.Controllers;

using AERIE.Application.Sync.DTOs;
using AERIE.Application.Sync.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] SyncPushRequest request, CancellationToken ct)
    {
        var result = await _syncService.PushSyncAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("pull")]
    public async Task<IActionResult> Pull([FromQuery] DateTime? since, CancellationToken ct)
    {
        var result = await _syncService.PullSyncAsync(since, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
