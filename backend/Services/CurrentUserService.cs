using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Backend.Config;
using Backend.Services.Interfaces;

namespace Backend.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? string.Empty;

    public Guid? UserGuid => Guid.TryParse(UserId, out var guid) ? guid : null;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin =>
        _httpContextAccessor.HttpContext?.User?.IsInRole(AppRoles.Admin) ?? false;

    public string? Email =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public string? FirstName =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.GivenName)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.GivenName);

    public string? LastName =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(JwtRegisteredClaimNames.FamilyName)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Surname);

    public string? FullName
    {
        get
        {
            var full = $"{FirstName} {LastName}".Trim();
            return string.IsNullOrEmpty(full) ? null : full;
        }
    }

    public IEnumerable<string> Roles =>
        _httpContextAccessor
            .HttpContext?.User?.Claims.Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
        ?? [];
}
