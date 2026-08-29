using Backend.Controllers;
using Backend.DTOs.Task;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backend.Tests.Controllers;

public class TasksControllerTests
{
    private readonly Mock<ITaskService> _taskService = new();
    private readonly TasksController _controller;

    public TasksControllerTests()
    {
        _controller = new TasksController(_taskService.Object);
    }

    private static TaskResponseDto SampleTask() =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = "Rebuild the homepage",
            Status = TaskItemStatus.Todo,
        };

    private static CreateTaskRequest CreateRequest() =>
        new("Rebuild the homepage", null, TaskItemStatus.Todo, null, null, null);

    private static UpdateTaskRequest UpdateRequest() =>
        new("Rebuild the homepage", null, TaskItemStatus.Done, null, null, null);

    [Fact]
    public async Task GetProjectTasks_ReturnsOkWithTasks()
    {
        var projectId = Guid.NewGuid();
        var tasks = new[] { SampleTask() };
        _taskService
            .Setup(s => s.GetProjectTasksAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        var result = await _controller.GetProjectTasks(projectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(tasks, okResult.Value);
    }

    [Fact]
    public async Task GetProjectTasks_ReturnsNotFoundWhenTheProjectIsUnreachable()
    {
        _taskService
            .Setup(s => s.GetProjectTasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<TaskResponseDto>?)null);

        var result = await _controller.GetProjectTasks(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateTask_ReturnsCreatedAtAction()
    {
        var task = SampleTask();
        _taskService
            .Setup(s =>
                s.CreateTaskAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CreateTaskRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(task);

        var result = await _controller.CreateTask(
            Guid.NewGuid(),
            CreateRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(TasksController.GetTaskById), created.ActionName);
        Assert.Equal(task, created.Value);
    }

    [Fact]
    public async Task CreateTask_ReturnsNotFoundWhenTheProjectIsUnreachable()
    {
        _taskService
            .Setup(s =>
                s.CreateTaskAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CreateTaskRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((TaskResponseDto?)null);

        var result = await _controller.CreateTask(
            Guid.NewGuid(),
            CreateRequest(),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetTaskById_ReturnsNotFoundWhenMissing()
    {
        _taskService
            .Setup(s => s.GetTaskByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskResponseDto?)null);

        var result = await _controller.GetTaskById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateTask_ReturnsOkWithTheUpdatedTask()
    {
        var task = SampleTask();
        _taskService
            .Setup(s =>
                s.UpdateTaskAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UpdateTaskRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(task);

        var result = await _controller.UpdateTask(task.Id, UpdateRequest(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(task, okResult.Value);
    }

    [Fact]
    public async Task DeleteTaskById_ReturnsNoContentOnSuccess()
    {
        _taskService
            .Setup(s => s.DeleteTaskByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteTaskById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteTaskById_ReturnsNotFoundWhenMissing()
    {
        _taskService
            .Setup(s => s.DeleteTaskByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteTaskById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetProjectDeletedTasks_ReturnsOkWithTheTrash()
    {
        var projectId = Guid.NewGuid();
        var tasks = new[] { SampleTask() };
        _taskService
            .Setup(s => s.GetProjectDeletedTasksAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        var result = await _controller.GetProjectDeletedTasks(projectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(tasks, okResult.Value);
    }

    [Fact]
    public async Task GetProjectDeletedTasks_ReturnsNotFoundWhenTheProjectIsUnreachable()
    {
        _taskService
            .Setup(s =>
                s.GetProjectDeletedTasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IEnumerable<TaskResponseDto>?)null);

        var result = await _controller.GetProjectDeletedTasks(
            Guid.NewGuid(),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetWorkspaceDeletedTasks_ReturnsOkWithTheTrash()
    {
        var workspaceId = Guid.NewGuid();
        var tasks = new[]
        {
            new WorkspaceTaskResponseDto { Id = Guid.NewGuid(), Title = "Gone" },
        };
        _taskService
            .Setup(s => s.GetWorkspaceDeletedTasksAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        var result = await _controller.GetWorkspaceDeletedTasks(
            workspaceId,
            CancellationToken.None
        );

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(tasks, okResult.Value);
    }

    // The null the service returns for a non-member has to become a 404 here. Without this,
    // deleting the guard ships a 200 with an empty body to someone outside the workspace,
    // which reads as "this workspace has thrown nothing away".
    [Fact]
    public async Task GetWorkspaceDeletedTasks_ReturnsNotFoundWhenTheWorkspaceIsUnreachable()
    {
        _taskService
            .Setup(s =>
                s.GetWorkspaceDeletedTasksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((IEnumerable<WorkspaceTaskResponseDto>?)null);

        var result = await _controller.GetWorkspaceDeletedTasks(
            Guid.NewGuid(),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task RestoreTaskById_ReturnsOkWithTheRestoredTask()
    {
        var task = SampleTask();
        _taskService
            .Setup(s => s.RestoreTaskByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var result = await _controller.RestoreTaskById(task.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(task, okResult.Value);
    }

    [Fact]
    public async Task RestoreTaskById_ReturnsNotFoundWhenMissing()
    {
        _taskService
            .Setup(s => s.RestoreTaskByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskResponseDto?)null);

        var result = await _controller.RestoreTaskById(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task MoveTask_ReturnsOkWithTheMovedTask()
    {
        var task = SampleTask();
        _taskService
            .Setup(s =>
                s.MoveTaskAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<MoveTaskRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(task);

        var result = await _controller.MoveTask(
            task.Id,
            new MoveTaskRequest(TaskItemStatus.InProgress, null, null),
            CancellationToken.None
        );

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(task, okResult.Value);
    }

    [Fact]
    public async Task MoveTask_ReturnsNotFoundWhenMissing()
    {
        _taskService
            .Setup(s =>
                s.MoveTaskAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<MoveTaskRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((TaskResponseDto?)null);

        var result = await _controller.MoveTask(
            Guid.NewGuid(),
            new MoveTaskRequest(TaskItemStatus.InProgress, null, null),
            CancellationToken.None
        );

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
