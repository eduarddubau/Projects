using Backend.Config;
using Backend.Data;
using Backend.DTOs.Dashboard;
using Backend.Mappings;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

/// <summary>The signed-in user's home, aggregated across their workspaces.</summary>
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly int _trashWindowDays;

    public DashboardService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IOptions<ProjectRetentionOptions> retentionOptions
    )
    {
        _context = context;
        _currentUser = currentUser;
        _trashWindowDays = retentionOptions.Value.TrashWindowDays;
    }

    public async Task<UserDashboardDto> GetMyDashboardAsync(CancellationToken ct = default)
    {
        var myProjects = _context.Projects.InWorkspacesOf(
            _context.WorkspaceMembers,
            _currentUser.UserGuid
        );

        var activeCount = await myProjects.CountAsync(ct);

        // Counts what the trash view shows: deletions still inside the retention window.
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);
        var deletedCount = await _context
            .Projects.IgnoreQueryFilters()
            .InWorkspacesOf(_context.WorkspaceMembers, _currentUser.UserGuid)
            .CountAsync(p => p.IsDeleted && p.DeletedAt >= cutoff, ct);

        var recentProjects = await myProjects
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Take(5)
            .MapToDto()
            .ToListAsync(ct);

        var latest = recentProjects.FirstOrDefault();

        return new UserDashboardDto
        {
            ActiveProjectCount = activeCount,
            DeletedProjectCount = deletedCount,
            LastActivityAt = latest is null ? null : latest.UpdatedAt ?? latest.CreatedAt,
            RecentProjects = recentProjects,
        };
    }
}
