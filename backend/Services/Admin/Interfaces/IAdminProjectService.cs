using Backend.DTOs.Project;

namespace Backend.Services.Admin.Interfaces;

public interface IAdminProjectService
{
    Task<int> RestoreProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IEnumerable<ProjectResponseDto>> GetAllDeletedProjectsAsync(
        CancellationToken ct = default
    );
    Task<int> PurgeProjectsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
