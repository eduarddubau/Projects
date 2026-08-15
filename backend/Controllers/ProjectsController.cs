using Backend.Config;
using Backend.DTOs.Project;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    // Leading slash escapes the api/[controller] prefix.
    [HttpGet("/api/workspaces/{workspaceId:guid}/projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetWorkspaceProjects(
        Guid workspaceId,
        CancellationToken ct
    )
    {
        var projects = await _projectService.GetWorkspaceProjectsAsync(workspaceId, ct);
        return Ok(projects);
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/projects/trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetWorkspaceDeletedProjects(
        Guid workspaceId,
        CancellationToken ct
    )
    {
        var trash = await _projectService.GetWorkspaceDeletedProjectsAsync(workspaceId, ct);
        return Ok(trash);
    }

    [HttpPost("/api/workspaces/{workspaceId:guid}/projects")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectResponseDto>> CreateProject(
        Guid workspaceId,
        CreateProjectRequest dto,
        CancellationToken ct
    )
    {
        var response = await _projectService.CreateProjectAsync(workspaceId, dto, ct);
        return CreatedAtAction(nameof(GetProjectById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> GetProjectById(
        Guid id,
        CancellationToken ct
    )
    {
        var project = await _projectService.GetProjectByIdAsync(id, ct);

        if (project is null)
            return NotFound();

        return Ok(project);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectResponseDto>> UpdateProject(
        Guid id,
        UpdateProjectRequest dto,
        CancellationToken ct
    )
    {
        var updated = await _projectService.UpdateProjectAsync(id, dto, ct);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProjectById(Guid id, CancellationToken ct)
    {
        var success = await _projectService.DeleteProjectByIdAsync(id, ct);

        if (!success)
            return NotFound(new { message = "Project not found." });

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectResponseDto>> RestoreProjectById(
        Guid id,
        CancellationToken ct
    )
    {
        var restoredProject = await _projectService.RestoreProjectByIdAsync(id, ct);

        if (restoredProject is null)
            return NotFound(new { message = "Project not found." });

        return Ok(restoredProject);
    }

    [HttpPost("{id:guid}/move")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectResponseDto>> MoveProject(
        Guid id,
        MoveProjectRequest dto,
        CancellationToken ct
    )
    {
        var moved = await _projectService.MoveProjectAsync(id, dto.WorkspaceId, ct);

        if (moved is null)
            return NotFound(new { message = "Project not found." });

        return Ok(moved);
    }
}
