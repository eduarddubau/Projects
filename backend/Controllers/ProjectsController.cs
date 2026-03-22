using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.DTOs;
using Backend.Services;

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
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetMyProjects()
    {
        var projects = await _projectService.GetMyProjectsAsync();

        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> GetMyProjectById(Guid id)
    {
        var project = await _projectService.GetMyProjectByIdAsync(id);

        if (project == null) return NotFound();

        return Ok(project);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectResponseDto>> CreateProject(CreateProjectDto dto)
    {
        var response = await _projectService.CreateProjectAsync(dto);

        return CreatedAtAction(nameof(GetMyProjectById), new { id = response.Id }, response);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> UpdateMyProject(Guid id, UpdateProjectDto dto)
    {
        var updated = await _projectService.UpdateMyProjectAsync(id, dto);

        if (updated == null) return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyProjectById(Guid id)
    {
        var success = await _projectService.DeleteMyProjectAsync(id);

        if (!success) return NotFound(new { message = "Project not found." });

        return NoContent();
    }

    // -------------------------------------------------------------------------
    // Admin endpoints
    // -------------------------------------------------------------------------

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet("admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllProjects()
    {
        var projects = await _projectService.GetAllProjectsAsync();

        return Ok(projects);
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet("admin/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> GetAnyProjectById(Guid id) // FIX #1: was IEnumerable<...>
    {
        var project = await _projectService.GetAnyProjectByIdAsync(id);

        if (project == null) return NotFound(new { message = "Project not found." }); // FIX #1: was missing

        return Ok(project);
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpDelete("admin/{id:guid}")] // FIX #5: removed redundant /delete suffix
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnyProjectById(Guid id)
    {
        var success = await _projectService.DeleteAnyProjectAsync(id);

        if (!success)
        {
            return NotFound(new { message = "Project not found." });
        }

        return NoContent();
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpPatch("admin/{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponseDto>> RestoreAnyProjectById(Guid id)
    {
        var restoredProject = await _projectService.RestoreAnyProjectAsync(id);

        if (restoredProject == null)
        {
            return NotFound(new { message = "Project not found." });
        }

        return Ok(restoredProject);
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet("admin/trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllDeletedProjects()
    {
        var trash = await _projectService.GetDeletedProjectsAsync();

        return Ok(trash);
    }
}