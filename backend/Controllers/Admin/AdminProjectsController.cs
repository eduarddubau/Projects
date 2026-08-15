using Backend.Config;
using Backend.DTOs.Project;
using Backend.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Admin;

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

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetProjects(
        CancellationToken ct
    )
    {
        var projects = await _projectService.GetAllProjectsAsync(ct);
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> GetProject(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetProjectByIdAsync(id, ct);

        if (project is null)
            return NotFound(new { message = "Project not found." });

        return Ok(project);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken ct)
    {
        var success = await _projectService.DeleteProjectByIdAsync(id, ct);

        if (!success)
            return NotFound(new { message = "Project not found." });

        return NoContent();
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

    [HttpGet("trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetDeletedProjects(
        CancellationToken ct
    )
    {
        var trash = await _projectService.GetAllDeletedProjectsAsync(ct);
        return Ok(trash);
    }

    [HttpPost("purge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PurgeProjects([FromBody] List<Guid> ids, CancellationToken ct)
    {
        var purgedCount = await _projectService.PurgeProjectsAsync(ids, ct);
        return Ok(new { purgedCount });
    }
}
