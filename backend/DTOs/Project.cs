namespace Backend.DTOs;

public record CreateProjectDto(string Name, string? Description);

public record UpdateProjectDto(string Name, string? Description);

public record ProjectResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
    public string? CreatedByDisplayName { get; init; }
    public string? UpdatedByDisplayName { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}