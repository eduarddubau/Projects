using Backend.Models;

namespace Backend.DTOs.Workspace;

public record InvitationResponseDto
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public string Email { get; init; } = string.Empty;
    public WorkspaceRole Role { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string InvitedByDisplayName { get; init; } = string.Empty;
}
