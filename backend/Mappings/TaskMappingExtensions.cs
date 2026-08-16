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
                t.Assignee == null ? null : t.Assignee.FirstName + " " + t.Assignee.LastName,
            StartDate = t.StartDate,
            DueDate = t.DueDate,
            CompletedAt = t.CompletedAt,
            IsDeleted = t.IsDeleted,
            DeletedAt = t.DeletedAt,
            CreatedAt = t.CreatedAt,
            CreatedBy = t.CreatedBy,
            CreatedByDisplayName =
                t.Creator == null ? string.Empty : t.Creator.FirstName + " " + t.Creator.LastName,
            UpdatedAt = t.UpdatedAt,
            UpdatedBy = t.UpdatedBy,
            UpdatedByDisplayName =
                t.Updater == null ? string.Empty : t.Updater.FirstName + " " + t.Updater.LastName,
        });
    }
}
