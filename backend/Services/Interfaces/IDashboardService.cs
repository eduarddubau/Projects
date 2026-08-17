using Backend.DTOs.Dashboard;

namespace Backend.Services.Interfaces;

public interface IDashboardService
{
    /// <summary>Null when the caller is not a member of the workspace.</summary>
    Task<WorkspaceDashboardDto?> GetWorkspaceDashboardAsync(
        Guid workspaceId,
        CancellationToken ct = default
    );
}
