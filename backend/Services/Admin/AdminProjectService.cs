using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services.Admin;

public class AdminProjectService : AdminTrashService<Project>, IAdminProjectService
{
    private readonly int _trashWindowDays;

    public AdminProjectService(
        AppDbContext context,
        IOptions<ProjectRetentionOptions> retentionOptions
    )
        : base(context)
    {
        _trashWindowDays = retentionOptions.Value.TrashWindowDays;
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(
        CancellationToken ct = default
    )
    {
        return await Context
            .Projects.IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .OrderByDescending(p => p.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<ProjectResponseDto?> GetProjectByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var project = await Context
            .Projects.IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project?.MapToDto();
    }

    public Task<bool> DeleteProjectByIdAsync(Guid id, CancellationToken ct = default) =>
        SoftDeleteByIdAsync(id, ct);

    public async Task<int> RestoreProjectsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var projects = await Context
            .Projects.IgnoreQueryFilters()
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

    public async Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(
        CancellationToken ct = default
    )
    {
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);

        var deleted = await Context
            .Projects.IgnoreQueryFilters()
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
        var projects = await Context
            .Projects.IgnoreQueryFilters()
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
