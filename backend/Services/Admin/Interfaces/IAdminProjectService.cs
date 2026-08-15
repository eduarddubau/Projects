using Backend.DTOs.Project;

namespace Backend.Services.Admin.Interfaces;

public interface IAdminProjectService
{
    Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync(CancellationToken ct = default);
    Task<ProjectResponseDto?> GetProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteProjectByIdAsync(Guid id, CancellationToken ct = default);
    Task<int> RestoreProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(
        CancellationToken ct = default
    );
    Task<int> PurgeProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
