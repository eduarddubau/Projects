using Backend.Models;

namespace Backend.DTOs.Workspace;

public record WorkspaceResponseDto : AuditResponseDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPersonal { get; init; }
    public WorkspaceRole MyRole { get; init; }
    public int MemberCount { get; init; }
    public int ProjectCount { get; init; }
}
