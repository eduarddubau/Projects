using Backend.Controllers;
using Backend.DTOs.Auth;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Backend.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<UserManager<User>> _userManager;
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var store = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _refreshTokenService
            .Setup(r => r.IssueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        _controller = new AuthController(
            _userManager.Object,
            Mock.Of<ILogger<AuthController>>(),
            _tokenService.Object,
            _refreshTokenService.Object,
            _currentUser.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        var user = new User { Email = "ada@example.com", FirstName = "Ada", LastName = "Lovelace" };
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "Str0ng!Pass")).ReturnsAsync(true);
        _tokenService.Setup(t => t.CreateToken(user)).ReturnsAsync("jwt-token");

        var result = await _controller.Login(new LoginRequest("ada@example.com", "Str0ng!Pass"), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        _userManager.Setup(m => m.FindByEmailAsync("missing@example.com")).ReturnsAsync((User?)null);

        var result = await _controller.Login(new LoginRequest("missing@example.com", "Str0ng!Pass"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var user = new User { Email = "ada@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "wrong")).ReturnsAsync(false);

        var result = await _controller.Login(new LoginRequest("ada@example.com", "wrong"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreatedWithToken()
    {
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), "Str0ng!Pass"))
            .ReturnsAsync(IdentityResult.Success);
        _userManager
            .Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _tokenService.Setup(t => t.CreateToken(It.IsAny<User>())).ReturnsAsync("jwt-token");

        var request = new RegisterRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Password = "Str0ng!Pass"
        };

        var result = await _controller.Register(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    }

    [Fact]
    public async Task Register_WhenCreateFails_ReturnsBadRequest()
    {
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email already taken." }));

        var request = new RegisterRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Password = "Str0ng!Pass"
        };

        var result = await _controller.Register(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsOkWithUser()
    {
        var user = new User { Email = "ada@example.com", FirstName = "Ada", LastName = "Lovelace" };
        _currentUser.Setup(c => c.UserId).Returns(user.Id.ToString());
        _userManager.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);

        var result = await _controller.GetCurrentUser(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserIdMissing_ReturnsUnauthorized()
    {
        _currentUser.Setup(c => c.UserId).Returns((string)null!);

        var result = await _controller.GetCurrentUser(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserNotFound_ReturnsNotFound()
    {
        _currentUser.Setup(c => c.UserId).Returns("missing-id");
        _userManager.Setup(m => m.FindByIdAsync("missing-id")).ReturnsAsync((User?)null);

        var result = await _controller.GetCurrentUser(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsOkWithNewTokens()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "ada@example.com" };
        _refreshTokenService
            .Setup(r => r.RotateAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshRotationResult(true, user.Id, "new-rt"));
        _userManager.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _tokenService.Setup(t => t.CreateToken(user)).ReturnsAsync("jwt-token");

        var result = await _controller.Refresh(new RefreshRequest("rt"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        _refreshTokenService
            .Setup(r => r.RotateAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RefreshRotationResult.Failure);

        var result = await _controller.Refresh(new RefreshRequest("bad"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Refresh_WhenUserGone_ReturnsUnauthorized()
    {
        var userId = Guid.NewGuid();
        _refreshTokenService
            .Setup(r => r.RotateAsync("rt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshRotationResult(true, userId, "new-rt"));
        _userManager.Setup(m => m.FindByIdAsync(userId.ToString())).ReturnsAsync((User?)null);

        var result = await _controller.Refresh(new RefreshRequest("rt"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Logout_RevokesTokenAndReturnsNoContent()
    {
        var result = await _controller.Logout(new RefreshRequest("rt"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        _refreshTokenService.Verify(r => r.RevokeAsync("rt", It.IsAny<CancellationToken>()), Times.Once);
    }
}
