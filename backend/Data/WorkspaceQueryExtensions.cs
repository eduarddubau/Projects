using Backend.Models;

namespace Backend.Data;

public static class WorkspaceQueryExtensions
{
    /// <summary>Projects in any workspace the user belongs to.</summary>
    // Not p.Workspace!.Members.Any(...): that emits an INNER JOIN against the soft-delete-filtered
    // workspaces, silently dropping projects. workspace_members has no filter.
    public static IQueryable<Project> InWorkspacesOf(
        this IQueryable<Project> query,
        IQueryable<WorkspaceMember> members,
        Guid? userId
    ) => query.Where(p => members.Any(m => m.WorkspaceId == p.WorkspaceId && m.UserId == userId));

    /// <summary>Projects whose workspace is in the given set.</summary>
    // Pass the filtered Workspaces to drop projects stranded inside a soft-deleted one:
    // deleting a workspace does not touch the projects in it, so they stay !IsDeleted and
    // an unqualified Projects query still counts work nobody can open. EXISTS rather than
    // the navigation, for the reason above.
    public static IQueryable<Project> InWorkspaces(
        this IQueryable<Project> query,
        IQueryable<Workspace> workspaces
    ) => query.Where(p => workspaces.Any(w => w.Id == p.WorkspaceId));
}
