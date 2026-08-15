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

    private static ProjectResponseDto SampleProject() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "My Project",
            Description = "A short description",
        };

    [Fact]
    public async Task GetWorkspaceProjects_ReturnsOkWithProjects()
    {
        var workspaceId = Guid.NewGuid();
        var projects = new[] { SampleProject() };
        _projectService
            .Setup(s => s.GetWorkspaceProjectsAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);

        var result = await _controller.GetWorkspaceProjects(workspaceId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(projects, okResult.Value);
    }

    [Fact]
    public async Task GetWorkspaceDeletedProjects_ReturnsOkWithProjects()
    {
        var workspaceId = Guid.NewGuid();
        var projects = new[] { SampleProject() };
        _projectService
            .Setup(s =>
                s.GetWorkspaceDeletedProjectsAsync(workspaceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(projects);

        var result = await _controller.GetWorkspaceDeletedProjects(
            workspaceId,
            CancellationToken.None
        );

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(projects, okResult.Value);
    }

    [Fact]
    public async Task GetProjectById_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        _projectService
            .Setup(s => s.GetProjectByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _controller.GetProjectById(project.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task GetProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService
            .Setup(s => s.GetProjectByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.GetProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateProject_ReturnsCreatedAtAction()
    {
        var workspaceId = Guid.NewGuid();
        var project = SampleProject();
        var request = new CreateProjectRequest(project.Name, project.Description);
        _projectService
            .Setup(s => s.CreateProjectAsync(workspaceId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _controller.CreateProject(workspaceId, request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ProjectsController.GetProjectById), createdResult.ActionName);
        Assert.Equal(project, createdResult.Value);
    }

    [Fact]
    public async Task UpdateProject_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        var request = new UpdateProjectRequest(project.Name, project.Description);
        _projectService
            .Setup(s => s.UpdateProjectAsync(project.Id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _controller.UpdateProject(project.Id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task UpdateProject_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var request = new UpdateProjectRequest("My Project", null);
        _projectService
            .Setup(s => s.UpdateProjectAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.UpdateProject(id, request, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteProjectById_WhenFound_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _projectService
            .Setup(s => s.DeleteProjectByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteProjectById(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService
            .Setup(s => s.DeleteProjectByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RestoreProjectById_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        _projectService
            .Setup(s => s.RestoreProjectByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await _controller.RestoreProjectById(project.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task RestoreProjectById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _projectService
            .Setup(s => s.RestoreProjectByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.RestoreProjectById(id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task MoveProject_WhenFound_ReturnsOk()
    {
        var project = SampleProject();
        var request = new MoveProjectRequest(Guid.NewGuid());
        _projectService
            .Setup(s =>
                s.MoveProjectAsync(project.Id, request.WorkspaceId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(project);

        var result = await _controller.MoveProject(project.Id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(project, okResult.Value);
    }

    [Fact]
    public async Task MoveProject_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var request = new MoveProjectRequest(Guid.NewGuid());
        _projectService
            .Setup(s => s.MoveProjectAsync(id, request.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectResponseDto?)null);

        var result = await _controller.MoveProject(id, request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

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
