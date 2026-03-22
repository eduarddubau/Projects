using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ProjectService : BaseService<Project>, IProjectService
{
    public ProjectService(AppDbContext context, ICurrentUserService currentUser)
        : base(context, currentUser) { }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync()
    {
        var projects = await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return projects.Select(MapToDto);
    }
    
    public async Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync()
    {
        var projects = await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.CreatedBy == _currentUser.UserGuid)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return projects.Select(MapToDto);
    }

    public async Task<ProjectResponseDto?> GetProjectByIdAsync(Guid id)
    {
        var project = await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid);

        return project is null ? null : MapToDto(project);
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(CreateProjectDto dto)
    {
        // Business Rule: Ensure the user doesn't already have a project with the same name
        bool nameExists = await _context.Projects
            .AnyAsync(p => p.Name == dto.Name && p.CreatedBy == _currentUser.UserGuid);

        if (nameExists)
        {
            throw new InvalidOperationException("You already have a project with this name.");
        }

        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        return MapToDto(project);
    }

    public async Task<bool> DeleteProjectAsync(Guid id)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid);

        if (project == null)
        {
            return false;
        }
        
        await _context.SaveChangesAsync();
        return true;
    }

    private static ProjectResponseDto MapToDto(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Description = project.Description,
        CreatedAt = project.CreatedAt,
        CreatedBy = project.CreatedBy,
        CreatedByDisplayName = project.Creator.GetDisplayName() ?? "System",
        UpdatedAt = project.UpdatedAt,
        UpdatedBy = project.UpdatedBy,
        UpdatedByDisplayName = project.UpdatedAt.HasValue 
            ? project.Updater.GetDisplayName() 
            : "Never"
    };

}