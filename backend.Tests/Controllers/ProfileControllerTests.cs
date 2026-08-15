using Backend.Controllers;
using Backend.DTOs.User;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Backend.Tests.Controllers;

public class ProfileControllerTests
{
    private readonly Mock<IProfileService> _profileService = new();
    private readonly ProfileController _controller;

    public ProfileControllerTests()
    {
        _controller = new ProfileController(
            _profileService.Object,
            Mock.Of<ILogger<ProfileController>>()
        );
    }

    private static UserResponseDto SampleProfile() =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = "ada@example.com",
            FirstName = "Ada",
            LastName = "Lovelace",
        };

    [Fact]
    public async Task GetProfile_WhenFound_ReturnsOk()
    {
        var profile = SampleProfile();
        _profileService
            .Setup(s => s.GetMyProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _controller.GetProfile(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(profile, okResult.Value);
    }

    [Fact]
    public async Task GetProfile_WhenNotFound_ReturnsNotFound()
    {
        _profileService
            .Setup(s => s.GetMyProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserResponseDto?)null);

        var result = await _controller.GetProfile(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateProfile_WhenFound_ReturnsOk()
    {
        var profile = SampleProfile();
        var request = new UpdateProfileRequest
        {
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Email = "me@example.com",
        };
        _profileService
            .Setup(s => s.UpdateMyProfileAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _controller.UpdateProfile(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(profile, okResult.Value);
    }

    [Fact]
    public async Task UpdateProfile_WhenNotFound_ReturnsNotFound()
    {
        var request = new UpdateProfileRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "me@example.com",
        };
        _profileService
            .Setup(s => s.UpdateMyProfileAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserResponseDto?)null);

        var result = await _controller.UpdateProfile(request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
