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
