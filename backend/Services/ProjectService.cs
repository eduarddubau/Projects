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
        return await Context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.CreatedBy == CurrentUser.UserGuid)
            .OrderByDescending(p => p.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetMyDeletedProjectsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);

        return await Context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.CreatedBy == CurrentUser.UserGuid && p.IsDeleted && p.DeletedAt >= cutoff)
            .OrderByDescending(p => p.DeletedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<ProjectResponseDto?> GetMyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await Context.Projects
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == CurrentUser.UserGuid, ct);

        return project?.MapToDto();
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(CreateProjectRequest dto, CancellationToken ct = default)
    {
        bool nameExists = await Context.Projects
            .AnyAsync(p => p.Name == dto.Name && p.CreatedBy == CurrentUser.UserGuid, ct);

        if (nameExists)
            throw new BusinessRuleException(BusinessRuleCodes.DuplicateProjectName,
                "You already have a project with this name.");

        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
        };

        Context.Projects.Add(project);
        await Context.SaveChangesAsync(ct);

        await Context.Entry(project).Reference(p => p.Creator).LoadAsync(ct);
        await Context.Entry(project).Reference(p => p.Updater).LoadAsync(ct);

        return project.MapToDto();
    }

    public async Task<ProjectResponseDto?> UpdateMyProjectAsync(Guid id, UpdateProjectRequest dto, CancellationToken ct = default)
    {
        var project = await Context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == CurrentUser.UserGuid, ct);

        if (project is null) return null;

        bool nameConflict = await Context.Projects
            .AnyAsync(p => p.Name == dto.Name
                        && p.CreatedBy == CurrentUser.UserGuid
                        && p.Id != id, ct);

        if (nameConflict)
            throw new BusinessRuleException(BusinessRuleCodes.DuplicateProjectName,
                "You already have a project with this name.");

        project.Name = dto.Name;
        project.Description = dto.Description;

        await Context.SaveChangesAsync(ct);

        await Context.Entry(project).Reference(p => p.Creator).LoadAsync(ct);
        await Context.Entry(project).Reference(p => p.Updater).LoadAsync(ct);

        return project.MapToDto();
    }

    public async Task<bool> DeleteMyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await Context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == CurrentUser.UserGuid, ct);

        if (project is null) return false;

        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;

        await Context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<ProjectResponseDto?> RestoreMyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await Context.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedBy == CurrentUser.UserGuid, ct);

        if (project is null) return null;

        if (project.IsDeleted)
        {
            project.IsDeleted = false;
            project.DeletedAt = null;
            await Context.SaveChangesAsync(ct);
        }

        await Context.Entry(project).Reference(p => p.Creator).LoadAsync(ct);
        await Context.Entry(project).Reference(p => p.Updater).LoadAsync(ct);

        return project.MapToDto();
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(CancellationToken ct = default)
    {
        return await Context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .OrderByDescending(p => p.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<ProjectResponseDto?> GetAnyProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await Context.Projects
            .IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project?.MapToDto();
    }

    public Task<bool> DeleteAnyProjectByIdAsync(Guid id, CancellationToken ct = default) => SoftDeleteAnyByIdAsync(id, ct);

    public async Task<int> RestoreAnyProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var projects = await Context.Projects
            .IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id) && p.IsDeleted)
            .ToListAsync(ct);

        foreach (var project in projects)
        {
            project.IsDeleted = false;
            project.DeletedAt = null;
        }

        await Context.SaveChangesAsync(ct);

        return projects.Count;
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);

        var deleted = await Context.Projects
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
        var projects = await Context.Projects
            .IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id) && p.IsDeleted)
            .ToListAsync(ct);

        foreach (var project in projects)
        {
            Context.MarkForHardDelete(project);
            Context.Projects.Remove(project);
        }

        await Context.SaveChangesAsync(ct);

        return projects.Count;
    }
}
