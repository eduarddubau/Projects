using Backend.Models;

namespace Backend.DTOs.Workspace;

public record WorkspaceMemberResponseDto
{
    public Guid WorkspaceId { get; init; }
    public Guid UserId { get; init; }
    public string UserDisplayName { get; init; } = string.Empty;
    public WorkspaceRole Role { get; init; }
    public DateTime JoinedAt { get; init; }
}
