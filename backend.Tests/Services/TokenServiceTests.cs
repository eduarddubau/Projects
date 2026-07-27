using Backend.Config;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace Backend.Tests.Services;

public class TokenServiceTests
{
    private readonly Mock<UserManager<User>> _userManager;
    private readonly TokenService _service;

    public TokenServiceTests()
    {
        var store = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var options = Options.Create(new JwtOptions
        {
            Key = new string('k', 64),
            Issuer = "test-issuer",
            Audience = "test-audience",
            DurationInMinutes = 60
        });

        _service = new TokenService(options, _userManager.Object);
    }

    [Fact]
    public async Task CreateToken_IncludesUserClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FirstName = "Ada",
            LastName = "Lovelace"
        };
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

        var token = await _service.CreateToken(user);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        Assert.Equal(user.Id.ToString(), jwt.GetClaim(JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.FirstName, jwt.GetClaim(JwtRegisteredClaimNames.GivenName).Value);
        Assert.Equal(user.LastName, jwt.GetClaim(JwtRegisteredClaimNames.FamilyName).Value);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
    }

    [Fact]
    public async Task CreateToken_WhenNicknameSet_IncludesNicknameClaim()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FirstName = "Ada",
            LastName = "Lovelace",
            Nickname = "Countess"
        };
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

        var token = await _service.CreateToken(user);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        Assert.Equal("Countess", jwt.GetClaim(JwtRegisteredClaimNames.Nickname).Value);
    }

    [Fact]
    public async Task CreateToken_WhenNicknameMissing_OmitsNicknameClaim()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FirstName = "Ada",
            LastName = "Lovelace"
        };
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

        var token = await _service.CreateToken(user);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Nickname);
    }

    [Fact]
    public async Task CreateToken_IncludesRoleClaims()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "ada@example.com", FirstName = "Ada", LastName = "Lovelace" };
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { AppRoles.Admin });

        var token = await _service.CreateToken(user);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        var roleClaims = jwt.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
        Assert.Contains(AppRoles.Admin, roleClaims);
    }
}
