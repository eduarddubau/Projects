using Backend.DTOs.Workspace;

namespace Backend.Services.Admin.Interfaces;

public interface IAdminWorkspaceService
{
    Task<IEnumerable<AdminWorkspaceResponseDto>> GetAllDeletedWorkspacesAsync(
        CancellationToken ct = default
    );
    Task<int> RestoreWorkspacesAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<int> PurgeWorkspacesAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
