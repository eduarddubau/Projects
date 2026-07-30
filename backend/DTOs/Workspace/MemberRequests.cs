using Backend.Models;

namespace Backend.DTOs.Workspace;

public record AddMemberRequest(Guid UserId, WorkspaceRole Role);
public record ChangeMemberRoleRequest(WorkspaceRole Role);
