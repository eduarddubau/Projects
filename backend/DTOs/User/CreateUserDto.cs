namespace Backend.DTOs.User;

public record CreateUserDto : IUserMapSource
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}