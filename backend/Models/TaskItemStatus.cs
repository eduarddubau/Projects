namespace Backend.Models;

// Todo is first so the zero value is the state a new task starts in.
public enum TaskItemStatus
{
    Todo,
    InProgress,
    Done,
}
