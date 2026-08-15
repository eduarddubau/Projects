using Backend.Controllers;
using Backend.DTOs.Dashboard;
using Backend.DTOs.Project;
using Backend.DTOs.User;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backend.Tests.Controllers;

public class DashboardControllerTests
{
    private readonly Mock<IDashboardService> _dashboardService = new();
    private readonly DashboardController _controller;

    public DashboardControllerTests()
    {
        _controller = new DashboardController(_dashboardService.Object);
    }

    [Fact]
    public async Task GetMyDashboard_ReturnsOkWithDashboard()
    {
        var dashboard = new UserDashboardDto
        {
            ActiveProjectCount = 2,
            DeletedProjectCount = 1,
            LastActivityAt = DateTime.UtcNow,
            RecentProjects = [new ProjectResponseDto { Id = Guid.NewGuid(), Name = "My Project" }],
        };
        _dashboardService
            .Setup(s => s.GetMyDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var result = await _controller.GetMyDashboard(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(dashboard, okResult.Value);
    }
}
