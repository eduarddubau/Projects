namespace Backend.DTOs.Project;

public record ProjectResponseDto : AuditResponseDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPurgeable { get; init; }
}
