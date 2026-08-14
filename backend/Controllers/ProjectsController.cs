using Backend.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Services.Interfaces;
using Backend.DTOs.Project;

namespace Backend.Controllers;

[Authorize(Policy = AppPolicies.StandardUser)]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetMyProjects(CancellationToken ct)
    {
        var projects = await _projectService.GetMyProjectsAsync(ct);
        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> GetMyProjectById(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetMyProjectByIdAsync(id, ct);

        if (project is null) return NotFound();

        return Ok(project);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectResponseDto>> CreateProject(CreateProjectRequest dto, CancellationToken ct)
    {
        var response = await _projectService.CreateProjectAsync(dto, ct);
        return CreatedAtAction(nameof(GetMyProjectById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> UpdateMyProject(Guid id, UpdateProjectRequest dto, CancellationToken ct)
    {
        var updated = await _projectService.UpdateMyProjectAsync(id, dto, ct);

        if (updated is null) return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyProjectById(Guid id, CancellationToken ct)
    {
        var success = await _projectService.DeleteMyProjectByIdAsync(id, ct);

        if (!success) return NotFound(new { message = "Project not found." });

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> RestoreMyProjectById(Guid id, CancellationToken ct)
    {
        var restoredProject = await _projectService.RestoreMyProjectByIdAsync(id, ct);

        if (restoredProject is null) return NotFound(new { message = "Project not found." });

        return Ok(restoredProject);
    }

    [HttpGet("trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetMyDeletedProjects(CancellationToken ct)
    {
        var trash = await _projectService.GetMyDeletedProjectsAsync(ct);
        return Ok(trash);
    }

    // -------------------------------------------------------------------------
    // Admin endpoints
    // -------------------------------------------------------------------------

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet("admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllProjects(CancellationToken ct)
    {
        var projects = await _projectService.GetAllProjectsAsync(ct);
        return Ok(projects);
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet("admin/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> GetAnyProjectById(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetAnyProjectByIdAsync(id, ct);

        if (project is null) return NotFound(new { message = "Project not found." });

        return Ok(project);
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpDelete("admin/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnyProjectById(Guid id, CancellationToken ct)
    {
        var success = await _projectService.DeleteAnyProjectByIdAsync(id, ct);

        if (!success) return NotFound(new { message = "Project not found." });

        return NoContent();
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpPost("admin/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreAnyProjects([FromBody] List<Guid> ids, CancellationToken ct)
    {
        var restoredCount = await _projectService.RestoreAnyProjectsAsync(ids, ct);
        return Ok(new { restoredCount });
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet("admin/trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllDeletedProjects(CancellationToken ct)
    {
        var trash = await _projectService.GetAllDeletedProjectsAsync(ct);
        return Ok(trash);
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpPost("admin/purge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PurgeProjects([FromBody] List<Guid> ids, CancellationToken ct)
    {
        var purgedCount = await _projectService.PurgeProjectsAsync(ids, ct);
        return Ok(new { purgedCount });
    }
}