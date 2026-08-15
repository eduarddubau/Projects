using Backend.Controllers;
using Backend.DTOs.User;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Backend.Tests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _controller = new UsersController(_userService.Object, Mock.Of<ILogger<UsersController>>());
    }

    private static UserResponseDto SampleUser() =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FirstName = "Ada",
            LastName = "Lovelace",
        };

    [Fact]
    public async Task GetUsers_ReturnsOkWithUsers()
    {
        var users = new[] { SampleUser() };
        _userService
            .Setup(s => s.GetAllUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _controller.GetUsers(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(users, okResult.Value);
    }

    [Fact]
    public async Task GetUser_WhenFound_ReturnsOk()
    {
        var user = SampleUser();
        _userService
            .Setup(s => s.GetAnyUserByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _controller.GetUser(user.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(user, okResult.Value);
    }

    [Fact]
    public async Task GetUser_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _userService
            .Setup(s => s.GetAnyUserByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserResponseDto?)null);

        var result = await _controller.GetUser(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateUser_ReturnsCreatedAtAction()
    {
        var user = SampleUser();
        var request = new CreateUserRequest
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
        };
        _userService
            .Setup(s => s.CreateUserAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _controller.CreateUser(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(UsersController.GetUser), createdResult.ActionName);
        Assert.Equal(user, createdResult.Value);
    }

    [Fact]
    public async Task DeleteUser_WhenFound_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _userService
            .Setup(s => s.DeleteAnyUserAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteUser(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteUser_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _userService
            .Setup(s => s.DeleteAnyUserAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteUser(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RestoreUser_WhenFound_ReturnsOk()
    {
        var user = SampleUser();
        _userService
            .Setup(s => s.RestoreAnyUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _controller.RestoreUser(user.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(user, okResult.Value);
    }

    [Fact]
    public async Task RestoreUser_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _userService
            .Setup(s => s.RestoreAnyUserAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserResponseDto?)null);

        var result = await _controller.RestoreUser(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDeletedUsers_ReturnsOkWithUsers()
    {
        var users = new[] { SampleUser() };
        _userService
            .Setup(s => s.GetDeletedUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _controller.GetDeletedUsers(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(users, okResult.Value);
    }
}
