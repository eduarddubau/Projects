using Backend.Controllers;
using Backend.DTOs.Dashboard;
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
    public async Task GetWorkspaceDashboard_ReturnsOkWithDashboard()
    {
        var workspaceId = Guid.NewGuid();
        var dashboard = new WorkspaceDashboardDto { OpenTaskCount = 12, MyOpenTaskCount = 5 };
        _dashboardService
            .Setup(s => s.GetWorkspaceDashboardAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var result = await _controller.GetWorkspaceDashboard(workspaceId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(dashboard, okResult.Value);
    }

    [Fact]
    public async Task GetWorkspaceDashboard_WhenServiceReturnsNull_Returns404()
    {
        _dashboardService
            .Setup(s =>
                s.GetWorkspaceDashboardAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((WorkspaceDashboardDto?)null);

        var result = await _controller.GetWorkspaceDashboard(
            Guid.NewGuid(),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
