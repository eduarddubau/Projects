namespace Backend.Services.Interfaces;

public interface ICurrentUserService
{
    string UserId { get; }
    Guid? UserGuid { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    string? Email { get; }
    string? FirstName { get; }
    string? LastName { get; }
    string? FullName { get; }
    IEnumerable<string> Roles { get; }
}
