using Backend.DTOs.Workspace;
using Backend.Models;

namespace Backend.Services.Interfaces;

public interface IWorkspaceService
{
    Task<IEnumerable<WorkspaceResponseDto>> GetMyWorkspacesAsync(CancellationToken ct = default);
    Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkspaceResponseDto> CreateWorkspaceAsync(CreateWorkspaceRequest dto, CancellationToken ct = default);
    Task<WorkspaceResponseDto> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest dto, CancellationToken ct = default);
    Task DeleteWorkspaceAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<WorkspaceResponseDto>> GetDeletedWorkspacesAsync(CancellationToken ct = default);
    Task<WorkspaceResponseDto> RestoreWorkspaceAsync(Guid id, CancellationToken ct = default);

    Task<IEnumerable<WorkspaceMemberResponseDto>> GetMembersAsync(Guid id, CancellationToken ct = default);
    Task<WorkspaceMemberResponseDto> AddMemberAsync(Guid id, AddMemberRequest dto, CancellationToken ct = default);
    Task<WorkspaceMemberResponseDto> ChangeRoleAsync(Guid id, Guid userId, WorkspaceRole newRole, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task LeaveAsync(Guid id, CancellationToken ct = default);
    
    Task EnsurePersonalWorkspaceAsync(User user, CancellationToken ct = default);
}
