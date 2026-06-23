using Backend.Controllers;
using Backend.DTOs.Project;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backend.Tests.Controllers;

public class ProjectsControllerTests
{
    private readonly Mock<IProjectService> _projectService = new();
    private readonly ProjectsController _controller;

    public ProjectsControllerTests()
    {
        _controller = new ProjectsController(_projectService.Object);
    }

    private static ProjectResponseDto SampleProject() => new()
    {
        Id = Guid.NewGuid(),
        Name = "My Project",
        Description = "A short description"
    };

    [Fact]
    public async Task GetMyProjects_ReturnsOkWithProjects()
    {
        var projects = new[] { SampleProject() };
        _projectService.Setup(s => s.GetMyProjectsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(projects);

        var result = await _controller.GetMyProjects(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(projects, okResult.Value);
    }

    [Fact]
    public async Task GetMyProjectById_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        _projectService.Setup(s => s.GetMyProjectByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await _controller.GetMyProjectById(project.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task GetMyProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService.Setup(s => s.GetMyProjectByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.GetMyProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateProject_ReturnsCreatedAtAction()
    {
        var project = SampleProject();
        var request = new CreateProjectRequest(project.Name, project.Description);
        _projectService.Setup(s => s.CreateProjectAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await _controller.CreateProject(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ProjectsController.GetMyProjectById), createdResult.ActionName);
        Assert.Equal(project, createdResult.Value);
    }

    [Fact]
    public async Task UpdateMyProject_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        var request = new UpdateProjectRequest(project.Name, project.Description);
        _projectService.Setup(s => s.UpdateMyProjectAsync(project.Id, request, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await _controller.UpdateMyProject(project.Id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task UpdateMyProject_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var request = new UpdateProjectRequest("My Project", null);
        _projectService.Setup(s => s.UpdateMyProjectAsync(id, request, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.UpdateMyProject(id, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteMyProjectById_WhenFound_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _projectService.Setup(s => s.DeleteMyProjectAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.DeleteMyProjectById(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteMyProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService.Setup(s => s.DeleteMyProjectAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.DeleteMyProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAllProjects_ReturnsOkWithProjects()
    {
        var projects = new[] { SampleProject() };
        _projectService.Setup(s => s.GetAllProjectsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(projects);

        var result = await _controller.GetAllProjects(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(projects, okResult.Value);
    }

    [Fact]
    public async Task GetAnyProjectById_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        _projectService.Setup(s => s.GetAnyProjectByIdAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await _controller.GetAnyProjectById(project.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task GetAnyProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService.Setup(s => s.GetAnyProjectByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.GetAnyProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteAnyProjectById_WhenFound_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _projectService.Setup(s => s.DeleteAnyProjectAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.DeleteAnyProjectById(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteAnyProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService.Setup(s => s.DeleteAnyProjectAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.DeleteAnyProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RestoreAnyProjectById_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        _projectService.Setup(s => s.RestoreAnyProjectAsync(project.Id, It.IsAny<CancellationToken>())).ReturnsAsync(project);

        var result = await _controller.RestoreAnyProjectById(project.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task RestoreAnyProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService.Setup(s => s.RestoreAnyProjectAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.RestoreAnyProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAllDeletedProjects_ReturnsOkWithProjects()
    {
        var projects = new[] { SampleProject() };
        _projectService.Setup(s => s.GetDeletedProjectsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(projects);

        var result = await _controller.GetAllDeletedProjects(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(projects, okResult.Value);
    }
}
