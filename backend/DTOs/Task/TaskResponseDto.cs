using Backend.Models;

namespace Backend.DTOs.Task;

public record TaskResponseDto : AuditResponseDto
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TaskItemStatus Status { get; init; }
    public int Position { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? AssigneeId { get; init; }

    /// <summary>Null when the assignee was soft-deleted: the User query filter hides the row, the FK survives.</summary>
    public string? AssigneeDisplayName { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public DateTime? CompletedAt { get; init; }
}
