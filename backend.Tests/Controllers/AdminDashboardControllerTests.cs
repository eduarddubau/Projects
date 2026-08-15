using Backend.Controllers.Admin;
using Backend.DTOs.Dashboard;
using Backend.DTOs.Project;
using Backend.DTOs.User;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backend.Tests.Controllers;

public class AdminDashboardControllerTests
{
    private readonly Mock<IDashboardService> _dashboardService = new();
    private readonly AdminDashboardController _controller;

    public AdminDashboardControllerTests()
    {
        _controller = new AdminDashboardController(_dashboardService.Object);
    }

    [Fact]
    public async Task GetAdminDashboard_ReturnsOkWithDashboard()
    {
        var dashboard = new AdminDashboardDto
        {
            ActiveProjectCount = 4,
            DeletedProjectCount = 2,
            ActiveUserCount = 3,
            DeletedUserCount = 1,
            RecentProjects = [new ProjectResponseDto { Id = Guid.NewGuid(), Name = "A Project" }],
            RecentUsers = [new UserResponseDto { Id = Guid.NewGuid(), Email = "ada@example.com" }],
        };
        _dashboardService
            .Setup(s => s.GetAdminDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var result = await _controller.GetAdminDashboard(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(dashboard, okResult.Value);
    }
}
