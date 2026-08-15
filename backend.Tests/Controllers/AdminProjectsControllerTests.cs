using Backend.Controllers.Admin;
using Backend.DTOs.Project;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backend.Tests.Controllers;

public class AdminProjectsControllerTests
{
    private readonly Mock<IProjectService> _projectService = new();
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
    public async Task GetAllProjects_ReturnsOkWithProjects()
    {
        var projects = new[] { SampleProject() };
        _projectService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);

        var result = await _controller.GetAllProjects(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(projects, okResult.Value);
    }

    [Fact]
    public async Task GetAnyProjectById_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        _projectService
            .Setup(s => s.GetAnyProjectByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _controller.GetAnyProjectById(project.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task GetAnyProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService
            .Setup(s => s.GetAnyProjectByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.GetAnyProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteAnyProjectById_WhenFound_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _projectService
            .Setup(s => s.DeleteAnyProjectByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteAnyProjectById(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteAnyProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService
            .Setup(s => s.DeleteAnyProjectByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteAnyProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RestoreAnyProjects_ReturnsOkWithRestoredCount()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _projectService
            .Setup(s => s.RestoreAnyProjectsAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _controller.RestoreAnyProjects(ids, CancellationToken.None);

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

        var result = await _controller.GetAllDeletedProjects(CancellationToken.None);

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
