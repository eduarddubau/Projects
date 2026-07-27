using Backend.DTOs.User;

namespace Backend.DTOs.Auth;

public record RegisterRequest : IUserMapSource
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Nickname { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
}