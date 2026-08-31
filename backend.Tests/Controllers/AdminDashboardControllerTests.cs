using Backend.Controllers.Admin;
using Backend.DTOs.Dashboard;
using Backend.DTOs.User;
using Backend.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backend.Tests.Controllers;

public class AdminDashboardControllerTests
{
    private readonly Mock<IAdminDashboardService> _dashboardService = new();
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
            ActiveUserCount = 3,
            SharedWorkspaceCount = 5,
            ActiveProjectCount = 4,
            TaskCount = 9,
            PurgeableProjectCount = 1,
            DeletedUserCount = 1,
            LockedOutUserCount = 0,
            DeletedProjectCount = 2,
            DeletedWorkspaceCount = 1,
            NewUserCount = 2,
            NewUserWindowDays = 7,
            Environment = "Testing",
            RecentUsers = [new UserResponseDto { Id = Guid.NewGuid(), Email = "ada@example.com" }],
        };
        _dashboardService
            .Setup(s => s.GetAdminDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var result = await _controller.GetDashboard(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(dashboard, okResult.Value);
    }
}
