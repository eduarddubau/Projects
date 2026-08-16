using Backend.Models;

namespace Backend.DTOs.Task;

public interface ITaskRequest
{
    string Title { get; }
    string? Description { get; }
    TaskItemStatus Status { get; }
    Guid? AssigneeId { get; }
    DateOnly? StartDate { get; }
    DateOnly? DueDate { get; }
}
