using Backend.DTOs.Task;

namespace Backend.Services.Interfaces;

public interface ITaskService
{
    Task<IEnumerable<TaskResponseDto>?> GetProjectTasksAsync(
        Guid projectId,
        CancellationToken ct = default
    );
    Task<TaskResponseDto?> GetTaskByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskResponseDto?> CreateTaskAsync(
        Guid projectId,
        CreateTaskRequest dto,
        CancellationToken ct = default
    );
    Task<TaskResponseDto?> UpdateTaskAsync(
        Guid id,
        UpdateTaskRequest dto,
        CancellationToken ct = default
    );
    Task<bool> DeleteTaskByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskResponseDto?> MoveTaskAsync(
        Guid id,
        MoveTaskRequest dto,
        CancellationToken ct = default
    );
}
