using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

// Inherits BaseService for the admin soft-delete and restore shared with UserService.
public class ProjectService : BaseService<Project>, IProjectService
{
    private readonly int _trashWindowDays;

    public ProjectService(AppDbContext context, ICurrentUserService currentUser, IOptions<ProjectRetentionOptions> retentionOptions)
        : base(context, currentUser)
    {
        _trashWindowDays = retentionOptions.Value.TrashWindowDays;
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync(CancellationToken ct = default)
    {
        return await _context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.CreatedBy == _currentUser.UserGuid)
            .OrderByDescending(p => p.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetMyDeletedProjectsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);

        return await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.CreatedBy == _currentUser.UserGuid && p.IsDeleted && p.DeletedAt >= cutoff)
            .OrderByDescending(p => p.DeletedAt)
            .MapToDto()
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

    public async Task<bool> DeleteMyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid, ct);

        if (project is null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<ProjectResponseDto?> RestoreMyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == _currentUser.UserGuid, ct);

        if (project is null) return null;

        if (project.IsDeleted)
        {
            project.IsDeleted = false;
            project.DeletedAt = null;
            await _context.SaveChangesAsync(ct);
        }

        await _context.Entry(project).Reference(p => p.Creator).LoadAsync(ct);
        await _context.Entry(project).Reference(p => p.Updater).LoadAsync(ct);

        return project.MapToDto();
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        return await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .OrderByDescending(p => p.CreatedAt)
            .MapToDto()
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

    public Task<bool> DeleteAnyProjectByIdAsync(Guid id, CancellationToken ct = default) => SoftDeleteAnyByIdAsync(id, ct);

    public async Task<int> RestoreAnyProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var projects = await _context.Projects
            .IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id) && p.IsDeleted)
            .ToListAsync(ct);

        foreach (var project in projects)
        {
            project.IsDeleted = false;
            project.DeletedAt = null;
        }

        await _context.SaveChangesAsync(ct);

        return projects.Count;
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);

        var deleted = await _context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .MapToDto()
            .ToListAsync(ct);

        return deleted.Select(p => p with { IsPurgeable = p.DeletedAt < cutoff });
    }

    public async Task<int> PurgeProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var projects = await _context.Projects
            .IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id) && p.IsDeleted)
            .ToListAsync(ct);

        foreach (var project in projects)
        {
            _context.MarkForHardDelete(project);
            _context.Projects.Remove(project);
        }

        await _context.SaveChangesAsync(ct);

        return projects.Count;
    }
}