using Backend.Data;
using Backend.DTOs;
using Backend.Mappings;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ProjectService : BaseService<Project>, IProjectService
{
    public ProjectService(AppDbContext context, ICurrentUserService currentUser)
        : base(context, currentUser) { }

    public async Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync()
    {
        return await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.CreatedBy == _currentUser.UserGuid)
            .OrderByDescending(p => p.CreatedAt)
            .ProjectToDto()
            .ToListAsync();
    }

    public async Task<ProjectResponseDto?> GetMyProjectByIdAsync(Guid id)
    {
        var project = await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid);

        return project is null ? null : project.MapToDto();
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(CreateProjectDto dto)
    {
        bool nameExists = await _context.Projects
            .AnyAsync(p => p.Name == dto.Name && p.CreatedBy == _currentUser.UserGuid);

        if (nameExists)
            throw new InvalidOperationException("You already have a project with this name.");

        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        await _context.Entry(project).Reference(p => p.Creator).LoadAsync();
        await _context.Entry(project).Reference(p => p.Updater).LoadAsync();

        return project.MapToDto();
    }

    public async Task<ProjectResponseDto?> UpdateMyProjectAsync(Guid id, UpdateProjectDto dto)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid);

        if (project is null) return null;

        bool nameConflict = await _context.Projects
            .AnyAsync(p => p.Name == dto.Name
                        && p.CreatedBy == _currentUser.UserGuid
                        && p.Id != id);

        if (nameConflict)
            throw new InvalidOperationException("You already have a project with this name.");

        project.Name = dto.Name;
        project.Description = dto.Description;

        await _context.SaveChangesAsync();

        await _context.Entry(project).Reference(p => p.Creator).LoadAsync();
        await _context.Entry(project).Reference(p => p.Updater).LoadAsync();

        return project.MapToDto();
    }

    public async Task<bool> DeleteMyProjectAsync(Guid id)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid);

        if (project is null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync()
    {
        return await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .OrderByDescending(p => p.CreatedAt)
            .ProjectToDto()
            .ToListAsync();
    }

    public async Task<ProjectResponseDto?> GetAnyProjectByIdAsync(Guid id)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id);

        return project is null ? null : project.MapToDto();
    }

    public async Task<bool> DeleteAnyProjectAsync(Guid id)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<ProjectResponseDto?> RestoreAnyProjectAsync(Guid id)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project is null) return null;

        if (!project.IsDeleted) return project.MapToDto();

        project.IsDeleted = false;
        project.DeletedAt = null;

        await _context.SaveChangesAsync();

        return project.MapToDto();
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetDeletedProjectsAsync()
    {
        return await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ProjectToDto()
            .ToListAsync();
    }
}