using Backend.Config;
using Backend.Data;
using Backend.DTOs.Dashboard;
using Backend.Mappings;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly int _trashWindowDays;

    public DashboardService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IOptions<ProjectRetentionOptions> retentionOptions)
    {
        _context = context;
        _currentUser = currentUser;
        _trashWindowDays = retentionOptions.Value.TrashWindowDays;
    }

    public async Task<UserDashboardDto> GetMyDashboardAsync(CancellationToken ct = default)
    {
        var myProjects = _context.Projects.Where(p => p.CreatedBy == _currentUser.UserGuid);

        var activeCount = await myProjects.CountAsync(ct);

        // Counts what the trash view shows: deletions still inside the retention window.
        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);
        var deletedCount = await _context.Projects
            .IgnoreQueryFilters()
            .CountAsync(p => p.CreatedBy == _currentUser.UserGuid && p.IsDeleted && p.DeletedAt >= cutoff, ct);

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
            RecentProjects = recentProjects
        };
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default)
    {
        var activeProjectCount = await _context.Projects.CountAsync(ct);

        // Admin trash has no retention cutoff — deleted projects stay until purged.
        var deletedProjectCount = await _context.Projects
            .IgnoreQueryFilters()
            .CountAsync(p => p.IsDeleted, ct);

        var activeUserCount = await _context.Users.CountAsync(ct);

        // Matches the users trash: anonymized accounts are hidden there.
        var deletedUserCount = await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.IsDeleted && !u.IsAnonymized, ct);

        var recentProjects = await _context.Projects
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .MapToDto()
            .ToListAsync(ct);

        var recentUsers = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .MapToDto()
            .ToListAsync(ct);

        return new AdminDashboardDto
        {
            ActiveProjectCount = activeProjectCount,
            DeletedProjectCount = deletedProjectCount,
            ActiveUserCount = activeUserCount,
            DeletedUserCount = deletedUserCount,
            RecentProjects = recentProjects,
            RecentUsers = recentUsers
        };
    }
}
