using Backend.Models;

namespace Backend.Services.Interfaces;

public interface IWorkspaceAccessService
{
    Task<WorkspaceRole> RequireMemberAsync(Guid workspaceId, CancellationToken ct = default);
    Task RequireOwnerAsync(Guid workspaceId, CancellationToken ct = default);
}
