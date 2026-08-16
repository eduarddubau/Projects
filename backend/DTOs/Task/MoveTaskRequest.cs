using Backend.Models;

namespace Backend.DTOs.Task;

/// <summary>Where the card was dropped, expressed as its neighbours rather than a position.</summary>
public record MoveTaskRequest(TaskItemStatus Status, Guid? PreviousTaskId, Guid? NextTaskId);
