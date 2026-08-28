using Backend.DTOs.Task;
using Backend.Models;

namespace Backend.Mappings;

public static class TaskMappingExtensions
{
    public static TaskResponseDto MapToDto(this TaskItem task) =>
        new()
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Position = task.Position,
            ProjectId = task.ProjectId,
            AssigneeId = task.AssigneeId,
            // Null, not "" like the audit names below: the client renders "unassigned" from it.
            AssigneeDisplayName = task.Assignee.GetDisplayName(),
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            IsDeleted = task.IsDeleted,
            DeletedAt = task.DeletedAt,
            CreatedAt = task.CreatedAt,
            CreatedBy = task.CreatedBy,
            CreatedByDisplayName = task.Creator.GetDisplayName() ?? string.Empty,
            UpdatedAt = task.UpdatedAt,
            UpdatedBy = task.UpdatedBy,
            UpdatedByDisplayName = task.Updater.GetDisplayName() ?? string.Empty,
        };

    // Duplicates the projection above rather than sharing it. Putting ProjectName on
    // TaskResponseDto instead would leave it silently empty on every path that does not
    // Include the project — create, update, move and get-by-id all map a loaded entity.
    public static IQueryable<WorkspaceTaskResponseDto> MapToWorkspaceDto(
        this IQueryable<TaskItem> query
    )
    {
        return query.Select(t => new WorkspaceTaskResponseDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status,
            Position = t.Position,
            ProjectId = t.ProjectId,
            ProjectName = t.Project == null ? string.Empty : t.Project.Name,
            AssigneeId = t.AssigneeId,
            AssigneeDisplayName =
                t.Assignee == null || t.Assignee.IsDeleted
                    ? null
                    : t.Assignee.FirstName + " " + t.Assignee.LastName,
            StartDate = t.StartDate,
            DueDate = t.DueDate,
            CompletedAt = t.CompletedAt,
            IsDeleted = t.IsDeleted,
            DeletedAt = t.DeletedAt,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            CreatedByDisplayName =
                t.Creator == null || t.Creator.IsDeleted
                    ? string.Empty
                    : t.Creator.FirstName + " " + t.Creator.LastName,
            UpdatedAt = t.UpdatedAt,
            UpdatedBy = t.UpdatedBy,
            UpdatedByDisplayName =
                t.Updater == null || t.Updater.IsDeleted
                    ? string.Empty
                    : t.Updater.FirstName + " " + t.Updater.LastName,
        });
    }

    // Every user navigation below tests IsDeleted itself rather than leaning on the User
    // query filter: the task trash reads through IgnoreQueryFilters(), which is query-wide
    // in EF and switches that filter off too. Without the test, a soft-deleted person's
    // real name surfaces there while every other read path returns null for them.
    public static IQueryable<TaskResponseDto> MapToDto(this IQueryable<TaskItem> query)
    {
        return query.Select(t => new TaskResponseDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status,
            Position = t.Position,
            ProjectId = t.ProjectId,
            AssigneeId = t.AssigneeId,
            AssigneeDisplayName =
                t.Assignee == null || t.Assignee.IsDeleted
                    ? null
                    : t.Assignee.FirstName + " " + t.Assignee.LastName,
            StartDate = t.StartDate,
            DueDate = t.DueDate,
            CompletedAt = t.CompletedAt,
            IsDeleted = t.IsDeleted,
            DeletedAt = t.DeletedAt,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            CreatedByDisplayName =
                t.Creator == null || t.Creator.IsDeleted
                    ? string.Empty
                    : t.Creator.FirstName + " " + t.Creator.LastName,
            UpdatedAt = t.UpdatedAt,
            UpdatedBy = t.UpdatedBy,
            UpdatedByDisplayName =
                t.Updater == null || t.Updater.IsDeleted
                    ? string.Empty
                    : t.Updater.FirstName + " " + t.Updater.LastName,
        });
    }
}
