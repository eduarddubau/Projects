using Backend.Config;
using Backend.DTOs.Workspace;
using Backend.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Admin;

[Authorize(Policy = AppPolicies.AdminOnly)]
[ApiController]
[Route("api/admin/workspaces")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class AdminWorkspacesController : ControllerBase
{
    private readonly IAdminWorkspaceService _workspaceService;

    public AdminWorkspacesController(IAdminWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AdminWorkspaceResponseDto>>> GetWorkspaces(
        CancellationToken ct
    )
    {
        return Ok(await _workspaceService.GetAllWorkspacesAsync(ct));
    }

    [HttpGet("trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AdminWorkspaceResponseDto>>> GetDeletedWorkspaces(
        CancellationToken ct
    )
    {
        return Ok(await _workspaceService.GetAllDeletedWorkspacesAsync(ct));
    }

    [HttpPost("restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreWorkspaces(
        [FromBody] List<Guid> ids,
        CancellationToken ct
    )
    {
        var restoredCount = await _workspaceService.RestoreWorkspacesAsync(ids, ct);
        return Ok(new { restoredCount });
    }

    // Safe only because a workspace holding any project, trashed included, cannot be
    // soft-deleted in the first place — so nothing here can orphan a project.
    [HttpPost("purge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PurgeWorkspaces(
        [FromBody] List<Guid> ids,
        CancellationToken ct
    )
    {
        var purgedCount = await _workspaceService.PurgeWorkspacesAsync(ids, ct);
        return Ok(new { purgedCount });
    }
}
