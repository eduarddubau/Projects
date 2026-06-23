using System.Security.Claims;
using Backend.Config;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace Backend.Tests.Services;

public class CurrentUserServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly CurrentUserService _service;

    public CurrentUserServiceTests()
    {
        _service = new CurrentUserService(_httpContextAccessor.Object);
    }

    private void SetUser(ClaimsPrincipal principal)
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = principal });
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(Guid userId, params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString())
        };
        claims.AddRange(extraClaims);

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void UserId_WhenSubClaimPresent_ReturnsItsValue()
    {
        var userId = Guid.NewGuid();
        SetUser(AuthenticatedPrincipal(userId));

        Assert.Equal(userId.ToString(), _service.UserId);
    }

    [Fact]
    public void UserId_WhenNoHttpContext_ReturnsEmptyString()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        Assert.Equal(string.Empty, _service.UserId);
    }

    [Fact]
    public void UserGuid_WhenSubClaimIsValidGuid_ReturnsParsedGuid()
    {
        var userId = Guid.NewGuid();
        SetUser(AuthenticatedPrincipal(userId));

        Assert.Equal(userId, _service.UserGuid);
    }

    [Fact]
    public void UserGuid_WhenNoHttpContext_ReturnsNull()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        Assert.Null(_service.UserGuid);
    }

    [Fact]
    public void IsAuthenticated_WhenIdentityIsAuthenticated_ReturnsTrue()
    {
        SetUser(AuthenticatedPrincipal(Guid.NewGuid()));

        Assert.True(_service.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_WhenNoHttpContext_ReturnsFalse()
    {
        _httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        Assert.False(_service.IsAuthenticated);
    }

    [Fact]
    public void IsAdmin_WhenUserHasAdminRole_ReturnsTrue()
    {
        SetUser(AuthenticatedPrincipal(Guid.NewGuid(), new Claim(ClaimTypes.Role, AppRoles.Admin)));

        Assert.True(_service.IsAdmin);
    }

    [Fact]
    public void IsAdmin_WhenUserHasNoAdminRole_ReturnsFalse()
    {
        SetUser(AuthenticatedPrincipal(Guid.NewGuid(), new Claim(ClaimTypes.Role, AppRoles.User)));

        Assert.False(_service.IsAdmin);
    }

    [Fact]
    public void Email_WhenEmailClaimPresent_ReturnsItsValue()
    {
        SetUser(AuthenticatedPrincipal(Guid.NewGuid(), new Claim(JwtRegisteredClaimNames.Email, "ada@example.com")));

        Assert.Equal("ada@example.com", _service.Email);
    }

    [Fact]
    public void FullName_WhenFirstAndLastNamePresent_ReturnsCombinedName()
    {
        SetUser(AuthenticatedPrincipal(
            Guid.NewGuid(),
            new Claim(JwtRegisteredClaimNames.GivenName, "Ada"),
            new Claim(JwtRegisteredClaimNames.FamilyName, "Lovelace")));

        Assert.Equal("Ada Lovelace", _service.FullName);
    }

    [Fact]
    public void FullName_WhenNoNameClaims_ReturnsNull()
    {
        SetUser(AuthenticatedPrincipal(Guid.NewGuid()));

        Assert.Null(_service.FullName);
    }

    [Fact]
    public void Roles_ReturnsAllRoleClaims()
    {
        SetUser(AuthenticatedPrincipal(
            Guid.NewGuid(),
            new Claim(ClaimTypes.Role, AppRoles.User),
            new Claim(ClaimTypes.Role, AppRoles.Admin)));

        Assert.Equal(new[] { AppRoles.User, AppRoles.Admin }, _service.Roles);
    }
}
