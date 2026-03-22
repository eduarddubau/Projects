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
    
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    
    public string CreatedByDisplayName { get; set; } = "System";
    public string UpdatedByDisplayName { get; set; } = "Never";
    
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}