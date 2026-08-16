using Backend.Controllers.Admin;
using Backend.DTOs.Project;
using Backend.Services.Admin.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backend.Tests.Controllers;

public class AdminProjectsControllerTests
{
    private readonly Mock<IAdminProjectService> _projectService = new();
    private readonly AdminProjectsController _controller;

    public AdminProjectsControllerTests()
    {
        _controller = new AdminProjectsController(_projectService.Object);
    }

    private static ProjectResponseDto SampleProject() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "My Project",
            Description = "A short description",
        };

    [Fact]
    public async Task RestoreAnyProjects_ReturnsOkWithRestoredCount()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _projectService
            .Setup(s => s.RestoreProjectsAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _controller.RestoreProjects(ids, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var restoredCount = okResult
            .Value!.GetType()
            .GetProperty("restoredCount")!
            .GetValue(okResult.Value);
        Assert.Equal(2, restoredCount);
    }

    [Fact]
    public async Task GetAllDeletedProjects_ReturnsOkWithProjects()
    {
        var projects = new[] { SampleProject() };
        _projectService
            .Setup(s => s.GetAllDeletedProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);

        var result = await _controller.GetDeletedProjects(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(projects, okResult.Value);
    }

    [Fact]
    public async Task PurgeProjects_ReturnsOkWithPurgedCount()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _projectService
            .Setup(s => s.PurgeProjectsAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _controller.PurgeProjects(ids, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var purgedCount = okResult
            .Value!.GetType()
            .GetProperty("purgedCount")!
            .GetValue(okResult.Value);
        Assert.Equal(2, purgedCount);
    }
}
