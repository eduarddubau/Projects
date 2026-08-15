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
}
