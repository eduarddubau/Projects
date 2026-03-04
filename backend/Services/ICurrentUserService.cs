namespace Backend.Services;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAdmin { get; }
}