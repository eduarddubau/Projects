namespace Backend.DTOs;

public record CreateUserDto : IUserMapSource
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}

public record UserResponseDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;

    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }

    public string? CreatedByDisplayName { get; init; }
    public string? UpdatedByDisplayName { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsDeleted { get; init; }
}