namespace Backend.DTOs.Task;

/// <summary>A task listed outside its own board, so it has to say which project it is in.</summary>
public record WorkspaceTaskResponseDto : TaskResponseDto
{
    public string ProjectName { get; init; } = string.Empty;
}
