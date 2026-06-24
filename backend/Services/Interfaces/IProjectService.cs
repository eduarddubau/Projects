using Backend.DTOs.Project;

namespace Backend.Services.Interfaces;

public interface IProjectService
{
    Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync(CancellationToken ct = default);
    Task<ProjectResponseDto?> GetMyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto> CreateProjectAsync(CreateProjectRequest dto, CancellationToken ct = default);
    Task<bool> DeleteMyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto?> UpdateMyProjectAsync(Guid id, UpdateProjectRequest dto, CancellationToken ct = default);

    Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(CancellationToken ct = default);
    Task<ProjectResponseDto?> GetAnyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAnyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto?> RestoreAnyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(CancellationToken ct = default);
}