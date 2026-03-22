namespace Backend.DTOs;

public class CreateUserDto
{
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

public class UserResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public string CreatedByDisplayName { get; set; } = "System";
    public string UpdatedByDisplayName { get; set; } = "Never";

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}