using Backend.Config;
using Backend.DTOs.Task;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[Authorize(Policy = AppPolicies.StandardUser)]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    // Leading slash escapes the api/[controller] prefix.
    [HttpGet("/api/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TaskResponseDto>>> GetProjectTasks(
        Guid projectId,
        CancellationToken ct
    )
    {
        var tasks = await _taskService.GetProjectTasksAsync(projectId, ct);

        if (tasks is null)
            return NotFound();

        return Ok(tasks);
    }

    [HttpPost("/api/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponseDto>> CreateTask(
        Guid projectId,
        CreateTaskRequest dto,
        CancellationToken ct
    )
    {
        var response = await _taskService.CreateTaskAsync(projectId, dto, ct);

        if (response is null)
            return NotFound();

        return CreatedAtAction(nameof(GetTaskById), new { id = response.Id }, response);
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/tasks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<WorkspaceTaskResponseDto>>> GetWorkspaceTasks(
        Guid workspaceId,
        [FromQuery] WorkspaceTaskQuery query,
        CancellationToken ct
    )
    {
        var tasks = await _taskService.GetWorkspaceTasksAsync(workspaceId, query, ct);
        return tasks is null ? NotFound() : Ok(tasks);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponseDto>> GetTaskById(Guid id, CancellationToken ct)
    {
        var task = await _taskService.GetTaskByIdAsync(id, ct);

        if (task is null)
            return NotFound();

        return Ok(task);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponseDto>> UpdateTask(
        Guid id,
        UpdateTaskRequest dto,
        CancellationToken ct
    )
    {
        var updated = await _taskService.UpdateTaskAsync(id, dto, ct);

        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTaskById(Guid id, CancellationToken ct)
    {
        var success = await _taskService.DeleteTaskByIdAsync(id, ct);

        if (!success)
            return NotFound(new { message = "Task not found." });

        return NoContent();
    }

    [HttpGet("/api/projects/{projectId:guid}/tasks/trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TaskResponseDto>>> GetProjectDeletedTasks(
        Guid projectId,
        CancellationToken ct
    )
    {
        var trash = await _taskService.GetProjectDeletedTasksAsync(projectId, ct);

        if (trash is null)
            return NotFound();

        return Ok(trash);
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponseDto>> RestoreTaskById(Guid id, CancellationToken ct)
    {
        var restored = await _taskService.RestoreTaskByIdAsync(id, ct);

        if (restored is null)
            return NotFound(new { message = "Task not found." });

        return Ok(restored);
    }

    [HttpPost("{id:guid}/move")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponseDto>> MoveTask(
        Guid id,
        MoveTaskRequest dto,
        CancellationToken ct
    )
    {
        var moved = await _taskService.MoveTaskAsync(id, dto, ct);

        if (moved is null)
            return NotFound(new { message = "Task not found." });

        return Ok(moved);
    }
}
