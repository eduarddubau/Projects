using Backend.Data;
using Backend.DTOs.Project;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ProjectService : BaseService<Project>, IProjectService
{
    public ProjectService(AppDbContext context, ICurrentUserService currentUser)
        : base(context, currentUser) { }

    public async Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync(CancellationToken ct = default)
    {
        return await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.CreatedBy == _currentUser.UserGuid)
            .OrderByDescending(p => p.CreatedAt)
            .ProjectToDto()
            .ToListAsync(ct);
    }

    public async Task<ProjectResponseDto?> GetMyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid, ct);

        return project is null ? null : project.MapToDto();
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(CreateProjectRequest dto, CancellationToken ct = default)
    {
        bool nameExists = await _context.Projects
            .AnyAsync(p => p.Name == dto.Name && p.CreatedBy == _currentUser.UserGuid, ct);

        if (nameExists)
            throw new BusinessRuleException("You already have a project with this name.");

        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);

        await _context.Entry(project).Reference(p => p.Creator).LoadAsync(ct);
        await _context.Entry(project).Reference(p => p.Updater).LoadAsync(ct);

        return project.MapToDto();
    }

    public async Task<ProjectResponseDto?> UpdateMyProjectAsync(Guid id, UpdateProjectRequest dto, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid, ct);

        if (project is null) return null;

        bool nameConflict = await _context.Projects
            .AnyAsync(p => p.Name == dto.Name
                        && p.CreatedBy == _currentUser.UserGuid
                        && p.Id != id, ct);

        if (nameConflict)
            throw new BusinessRuleException("You already have a project with this name.");

        project.Name = dto.Name;
        project.Description = dto.Description;

        await _context.SaveChangesAsync(ct);

        await _context.Entry(project).Reference(p => p.Creator).LoadAsync(ct);
        await _context.Entry(project).Reference(p => p.Updater).LoadAsync(ct);

        return project.MapToDto();
    }

    public async Task<bool> DeleteMyProjectAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid, ct);

        if (project is null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        return await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .OrderByDescending(p => p.CreatedAt)
            .ProjectToDto()
            .ToListAsync(ct);
    }

    public async Task<ProjectResponseDto?> GetAnyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project is null ? null : project.MapToDto();
    }

    public async Task<bool> DeleteAnyProjectAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<ProjectResponseDto?> RestoreAnyProjectAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null) return null;

        if (!project.IsDeleted) return project.MapToDto();

        project.IsDeleted = false;
        project.DeletedAt = null;

        await _context.SaveChangesAsync(ct);

        return project.MapToDto();
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetDeletedProjectsAsync(CancellationToken ct = default)
    {
        return await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ProjectToDto()
            .ToListAsync(ct);
    }
}