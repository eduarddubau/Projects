using Backend.DTOs.Project;

namespace Backend.Services.Interfaces;

public interface IProjectService
{
    Task<IEnumerable<ProjectResponseDto>> GetWorkspaceProjectsAsync(
        Guid workspaceId,
        CancellationToken ct = default
    );
    Task<IEnumerable<ProjectResponseDto>> GetWorkspaceTrashAsync(
        Guid workspaceId,
        CancellationToken ct = default
    );
    Task<ProjectResponseDto?> GetProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto> CreateProjectAsync(
        Guid workspaceId,
        CreateProjectRequest dto,
        CancellationToken ct = default
    );
    Task<ProjectResponseDto?> UpdateProjectAsync(
        Guid id,
        UpdateProjectRequest dto,
        CancellationToken ct = default
    );
    Task<bool> DeleteProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto?> RestoreProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto?> MoveProjectAsync(
        Guid id,
        Guid targetWorkspaceId,
        CancellationToken ct = default
    );

    Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(CancellationToken ct = default);
    Task<ProjectResponseDto?> GetAnyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAnyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> RestoreAnyProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(
        CancellationToken ct = default
    );
    Task<int> PurgeProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
