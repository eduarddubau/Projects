namespace Backend.DTOs.Workspace;

/// <summary>Deliberately without MyRole: an admin belongs to no workspace, and the
/// projected enum would come back as its zero value, reading as Member.</summary>
public record AdminWorkspaceResponseDto : AuditResponseDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPersonal { get; init; }
    public int MemberCount { get; init; }
    public int ProjectCount { get; init; }
}
