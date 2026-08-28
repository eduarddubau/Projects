namespace Backend.DTOs.Task;

public enum TaskAssigneeFilter
{
    Any,
    Me,
    Unassigned,
}

/// <summary>Narrows the workspace task list. Done tasks are never included — the board is where those live.</summary>
public record WorkspaceTaskQuery
{
    public TaskAssigneeFilter Assignee { get; init; } = TaskAssigneeFilter.Any;

    /// <summary>Only tasks due strictly before this day. The caller passes its own date:
    /// a due date is a calendar day, and the server's UTC one is a different day for
    /// three hours out of every twenty-four here.</summary>
    public DateOnly? DueBefore { get; init; }
}
