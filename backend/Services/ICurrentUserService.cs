namespace Backend.Services;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAdmin { get; }
    string? Email { get; }
    string? FirstName { get; }
    string? LastName { get; }
    IEnumerable<string> Roles { get; }
}