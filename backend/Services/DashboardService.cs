using Backend.Data;
using Backend.DTOs.Dashboard;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>The numbers on one workspace's home. Scoped to a single workspace, never
/// aggregated across them: the page they feed acts on one workspace, so counting more
/// than that would make the tiles and the buttons disagree.</summary>
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<WorkspaceDashboardDto?> GetWorkspaceDashboardAsync(
        Guid workspaceId,
        CancellationToken ct = default
    )
    {
        var userId = _currentUser.UserGuid;

        // Null, not zeroes: a workspace the caller cannot reach is a 404, not an empty one.
        var isMember = await _context.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == workspaceId && m.UserId == userId,
            ct
        );
        if (!isMember)
            return null;

        // Carries the Projects query filter, so a trashed project's tasks drop out of
        // both counts and come back on restore, with nothing written either way.
        var workspaceProjects = _context.Projects.Where(p => p.WorkspaceId == workspaceId);

        var openTasks = _context
            .Tasks.InProjectsOf(workspaceProjects)
            .Where(t => t.Status != TaskItemStatus.Done);

        var openTaskCount = await openTasks.CountAsync(ct);
        var myOpenTaskCount = await openTasks.CountAsync(t => t.AssigneeId == userId, ct);

        return new WorkspaceDashboardDto
        {
            OpenTaskCount = openTaskCount,
            MyOpenTaskCount = myOpenTaskCount,
        };
    }
}
