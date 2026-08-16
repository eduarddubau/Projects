using Backend.Models;

namespace Backend.Data;

public static class TaskQueryExtensions
{
    /// <summary>Tasks whose project the caller can reach.</summary>
    // EXISTS rather than a join, for the reason WorkspaceQueryExtensions documents. Passing the
    // filtered Projects set is what hides a trashed project's tasks without writing to them.
    public static IQueryable<TaskItem> InProjectsOf(
        this IQueryable<TaskItem> query,
        IQueryable<Project> accessibleProjects
    ) => query.Where(t => accessibleProjects.Any(p => p.Id == t.ProjectId));
}
