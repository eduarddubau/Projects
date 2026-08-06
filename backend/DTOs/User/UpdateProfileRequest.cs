namespace Backend.DTOs.User;

public record UpdateProfileRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public string? Nickname { get; init; }
}
