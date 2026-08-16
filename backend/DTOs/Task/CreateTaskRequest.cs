using Backend.Models;

namespace Backend.DTOs.Task;

public record CreateTaskRequest(
    string Title,
    string? Description,
    TaskItemStatus Status,
    Guid? AssigneeId,
    DateOnly? StartDate,
    DateOnly? DueDate
) : ITaskRequest;
