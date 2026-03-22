using Backend.DTOs.Project;

namespace Backend.Services.Interfaces;

public interface IProjectService
{
    Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync(CancellationToken ct = default);
    Task<ProjectResponseDto?> GetMyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto> CreateProjectAsync(CreateProjectDto dto, CancellationToken ct = default);
    Task<bool> DeleteMyProjectAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto?> UpdateMyProjectAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default);

    Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(CancellationToken ct = default);
    Task<ProjectResponseDto?> GetAnyProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteAnyProjectAsync(Guid id, CancellationToken ct = default);
    Task<ProjectResponseDto?> RestoreAnyProjectAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ProjectResponseDto>> GetDeletedProjectsAsync(CancellationToken ct = default);
}