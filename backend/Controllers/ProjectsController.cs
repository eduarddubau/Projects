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
    public async Task<ActionResult<IEnumerable<ProjectResponseDto>>> GetProjects()
    {
        var projects = await _projectService.GetMyProjectsAsync();

        var response = projects.Select(p => new ProjectResponseDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy
        }).ToList();

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectResponseDto>> GetProject(Guid id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        
        if (project == null) return NotFound();

        var response = new ProjectResponseDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            CreatedBy = project.CreatedBy
        };

        return Ok(response);
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
}