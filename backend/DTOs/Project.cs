namespace Backend.DTOs;

public class CreateProjectDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class ProjectResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}