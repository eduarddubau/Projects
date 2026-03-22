using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.DTOs;
using Backend.Services;

namespace Backend.Controllers;

[Authorize(Policy = AppPolicies.StandardUser)]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [Route("all")]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetAllProjects()
    {
        var projects = await _projectService.GetAllProjectsAsync();

        return Ok(projects);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetProjects()
    {
        var projects = await _projectService.GetMyProjectsAsync();

        return Ok(projects);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectResponseDto>> GetProject(Guid id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        
        if (project == null) return NotFound();

        return Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponseDto>> CreateProject(CreateProjectDto dto)
    {
        var project = await _projectService.CreateProjectAsync(dto);
        
        var response = new ProjectResponseDto 
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            CreatedBy = project.CreatedBy
        };

        return CreatedAtAction(nameof(GetProject), new { id = response.Id }, response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var success = await _projectService.DeleteProjectAsync(id);

        if (!success)
        {
            return NotFound(new { message = "Project not found or you do not have permission to delete it." });
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/restore")]
    public async Task<ActionResult<ProjectResponseDto>> RestoreProject(Guid id)
    {
        var restoredProject = await _projectService.RestoreProjectAsync(id);

        if (restoredProject == null)
        {
            return NotFound(new { message = "Project not found or you do not have permission to restore it." });
        }

        return Ok(restoredProject);
    }
}