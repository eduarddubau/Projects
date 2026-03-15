using System.Security.Claims;
using Backend.Config;

namespace Backend.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAdmin => _httpContextAccessor.HttpContext?.User?.IsInRole(AppRoles.Admin) ?? false;

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public string? FirstName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.GivenName);

    public string? LastName => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Surname);

    public IEnumerable<string> Roles => _httpContextAccessor.HttpContext?.User?.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value) ?? Enumerable.Empty<string>();
}