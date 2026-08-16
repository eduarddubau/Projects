using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services.Admin;

public class AdminProjectService : IAdminProjectService
{
    private readonly AppDbContext _context;
    private readonly int _trashWindowDays;

    public AdminProjectService(
        AppDbContext context,
        IOptions<ProjectRetentionOptions> retentionOptions
    )
    {
        _context = context;
        _trashWindowDays = retentionOptions.Value.TrashWindowDays;
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
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);

        var deleted = await _context
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
        var projects = await _context
            .Projects.IgnoreQueryFilters()
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
