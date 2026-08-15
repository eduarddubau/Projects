using Backend.Models;

namespace Backend.DTOs.Workspace;

// Nullable: System.Text.Json binds an omitted role to Member, the zero value.
public record AddMemberRequest(Guid UserId, WorkspaceRole? Role);

public record ChangeMemberRoleRequest(WorkspaceRole? Role);
