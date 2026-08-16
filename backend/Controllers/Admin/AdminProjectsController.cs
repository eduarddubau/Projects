using Backend.Config;
using Backend.DTOs.Project;
using Backend.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Admin;

/// <summary>The project trash. Live projects are reached through their workspace,
/// so there is nothing here that reads one.</summary>
[Authorize(Policy = AppPolicies.AdminOnly)]
[ApiController]
[Route("api/admin/projects")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class AdminProjectsController : ControllerBase
{
    private readonly IAdminProjectService _projectService;

    public AdminProjectsController(IAdminProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet("trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetDeletedProjects(
        CancellationToken ct
    )
    {
        var trash = await _projectService.GetAllDeletedProjectsAsync(ct);
        return Ok(trash);
    }

    [HttpPost("restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreProjects(
        [FromBody] List<Guid> ids,
        CancellationToken ct
    )
    {
        var restoredCount = await _projectService.RestoreProjectsAsync(ids, ct);
        return Ok(new { restoredCount });
    }

    [HttpPost("purge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PurgeProjects([FromBody] List<Guid> ids, CancellationToken ct)
    {
        var purgedCount = await _projectService.PurgeProjectsAsync(ids, ct);
        return Ok(new { purgedCount });
    }
}
