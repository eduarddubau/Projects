using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Admin;

public class AdminProjectService : IAdminProjectService
{
    private readonly AppDbContext _context;
    private readonly TrashWindow _trashWindow;

    public AdminProjectService(AppDbContext context, TrashWindow trashWindow)
    {
        _context = context;
        _trashWindow = trashWindow;
    }

    public async Task<int> RestoreProjectsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var projects = await _context
            .Projects.IgnoreQueryFilters()
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

    public async Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(
        CancellationToken ct = default
    )
    {
        var cutoff = _trashWindow.Cutoff;

        var deleted = await _context
            .Projects.IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .MapToDto()
            .ToListAsync(ct);

        return deleted.Select(p => p with { IsPurgeable = p.DeletedAt < cutoff });
    }

    public async Task<int> PurgeProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var projects = await _context
            .Projects.IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id) && p.IsDeleted)
            .ToListAsync(ct);

        var projectIds = projects.Select(p => p.Id).ToList();

        // Tasks hold their project by a Restrict FK, so they must go first or the delete throws.
        // Explicit rather than DeleteBehavior.Cascade: a database-level cascade would bypass the
        // SaveChangesAsync interceptor entirely, and purging is the one place destruction is
        // intended, so it should read as a deliberate line. IgnoreQueryFilters because a
        // soft-deleted task holds the FK just as hard as a live one.
        var tasks = await _context
            .Tasks.IgnoreQueryFilters()
            .Where(t => projectIds.Contains(t.ProjectId))
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            _context.MarkForHardDelete(task);
            _context.Tasks.Remove(task);
        }

        foreach (var project in projects)
        {
            _context.MarkForHardDelete(project);
            _context.Projects.Remove(project);
        }

        await _context.SaveChangesAsync(ct);

        return projects.Count;
    }
}
