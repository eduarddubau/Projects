using Backend.Config;
using Backend.Data;
using Backend.DTOs.Dashboard;
using Backend.Mappings;
using Backend.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Admin;

public class AdminDashboardService : IAdminDashboardService
{
    /// <summary>How far back a signup still counts as new; published on the DTO so no client assumes it.</summary>
    // Changing it means re-reading the Romanian copy: `stats.newUsers` is inflected on this
    // number, not on the signup count, and above 19 Romanian needs "de zile".
    private const int NewUserWindowDays = 7;

    private const int RecentUserCount = 5;

    private readonly AppDbContext _context;
    private readonly TrashWindow _trashWindow;
    private readonly IHostEnvironment _environment;

    public AdminDashboardService(
        AppDbContext context,
        TrashWindow trashWindow,
        IHostEnvironment environment
    )
    {
        _context = context;
        _trashWindow = trashWindow;
        _environment = environment;
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default)
    {
        var activeUserCount = await _context.Users.CountAsync(ct);

        // Shared only: every account holds one undeletable personal workspace, so a total
        // would move in lockstep with the user count above it.
        var sharedWorkspaceCount = await _context.Workspaces.CountAsync(w => !w.IsPersonal, ct);

        // Through the filtered Workspaces: GDPR erasure soft-deletes the account's personal
        // workspace without touching the projects inside it, so those rows stay !IsDeleted
        // and an unqualified count reports projects nothing in the app can reach.
        var liveProjects = _context.Projects.InWorkspaces(_context.Workspaces);
        var activeProjectCount = await liveProjects.CountAsync(ct);

        // Through liveProjects, not a bare count: soft-deleting a project leaves its tasks
        // !IsDeleted, and reading through liveProjects carries the workspace guard down too.
        var taskCount = await _context.Tasks.InProjectsOf(liveProjects).CountAsync(ct);

        // AdminProjectService.PurgeProjectsAsync filters on this same cutoff, so the number
        // and the button behind it cannot disagree.
        var cutoff = _trashWindow.Cutoff;
        var purgeableProjectCount = await _context
            .Projects.IgnoreQueryFilters()
            .CountAsync(p => p.IsDeleted && p.DeletedAt < cutoff, ct);

        // Matches the users trash: anonymized accounts are hidden there, being past recall.
        var deletedUserCount = await _context
            .Users.IgnoreQueryFilters()
            .CountAsync(u => u.IsDeleted && !u.IsAnonymized, ct);

        // Both halves, and the same boundary, as UserManager.IsLockedOutAsync: an account
        // with lockout disabled signs in fine however future its LockoutEnd is. The filtered
        // set, so a deleted account's stale lockout is not counted as a live one.
        var now = DateTimeOffset.UtcNow;
        var lockedOutUserCount = await _context.Users.CountAsync(
            u => u.LockoutEnabled && u.LockoutEnd >= now,
            ct
        );

        // The admin trash has no window — deleted projects stay listed until purged.
        var deletedProjectCount = await _context
            .Projects.IgnoreQueryFilters()
            .CountAsync(p => p.IsDeleted, ct);

        var deletedWorkspaceCount = await _context
            .Workspaces.IgnoreQueryFilters()
            .CountAsync(w => w.IsDeleted, ct);

        var newUserSince = DateTime.UtcNow.AddDays(-NewUserWindowDays);
        var newUserCount = await _context.Users.CountAsync(u => u.CreatedAt >= newUserSince, ct);

        var recentUsers = await _context
            .Users.OrderByDescending(u => u.CreatedAt)
            .Take(RecentUserCount)
            .MapToDto()
            .ToListAsync(ct);

        return new AdminDashboardDto
        {
            ActiveUserCount = activeUserCount,
            SharedWorkspaceCount = sharedWorkspaceCount,
            ActiveProjectCount = activeProjectCount,
            TaskCount = taskCount,
            PurgeableProjectCount = purgeableProjectCount,
            DeletedUserCount = deletedUserCount,
            LockedOutUserCount = lockedOutUserCount,
            DeletedProjectCount = deletedProjectCount,
            DeletedWorkspaceCount = deletedWorkspaceCount,
            NewUserCount = newUserCount,
            NewUserWindowDays = NewUserWindowDays,
            Environment = _environment.EnvironmentName,
            RecentUsers = recentUsers,
        };
    }
}
