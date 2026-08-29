using Backend.Config;
using Backend.Data;
using Backend.DTOs.Task;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

/// <summary>Tasks the caller can reach through the workspace holding their project.</summary>
// No owner check anywhere, deliberately: any member may create, edit, move, delete and
// restore a task. Safe only because deleting is recoverable — take the trash away and this
// policy has to change with it.
public class TaskService : ITaskService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly int _trashWindowDays;

    public TaskService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IOptions<RetentionOptions> retentionOptions
    )
    {
        _context = context;
        _currentUser = currentUser;
        _trashWindowDays = retentionOptions.Value.TrashWindowDays;
    }

    // Carries the Projects query filter, so tasks of a trashed project fall out of every
    // read below and come back on restore, with nothing written either way.
    private IQueryable<Project> AccessibleProjects =>
        _context.Projects.InWorkspacesOf(_context.WorkspaceMembers, _currentUser.UserGuid);

    private IQueryable<TaskItem> AccessibleTasks => _context.Tasks.InProjectsOf(AccessibleProjects);

    public async Task<IEnumerable<TaskResponseDto>?> GetProjectTasksAsync(
        Guid projectId,
        CancellationToken ct = default
    )
    {
        // Null, not empty: a project the caller cannot reach is a 404, not a project with no tasks.
        if (!await AccessibleProjects.AnyAsync(p => p.Id == projectId, ct))
            return null;

        return await _context
            .Tasks.Where(t => t.ProjectId == projectId)
            // Not OrderBy(t => t.Status): the enum persists as a string, so Postgres sorts it
            // alphabetically (Done, InProgress, Todo) while the in-memory provider sorts by
            // enum value. Keep it inline — EF cannot translate a call to a helper.
            .OrderBy(t =>
                t.Status == TaskItemStatus.Todo ? 0
                : t.Status == TaskItemStatus.InProgress ? 1
                : 2
            )
            .ThenBy(t => t.Position)
            .ThenBy(t => t.Id)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<WorkspaceTaskResponseDto>?> GetWorkspaceTasksAsync(
        Guid workspaceId,
        WorkspaceTaskQuery query,
        CancellationToken ct = default
    )
    {
        var userId = _currentUser.UserGuid;

        if (!await IsMemberAsync(workspaceId, ct))
            return null;

        // Carries the Projects query filter, so a trashed project's tasks drop out here
        // and come back on restore, with nothing written either way.
        var tasks = _context
            .Tasks.InProjectsOf(_context.Projects.Where(p => p.WorkspaceId == workspaceId))
            .Where(t => t.Status != TaskItemStatus.Done);

        tasks = query.Assignee switch
        {
            TaskAssigneeFilter.Me => tasks.Where(t => t.AssigneeId == userId),
            TaskAssigneeFilter.Unassigned => tasks.Where(t => t.AssigneeId == null),
            _ => tasks,
        };

        if (query.DueBefore is DateOnly cutoff)
            tasks = tasks.Where(t => t.DueDate != null && t.DueDate < cutoff);

        return await tasks
            // Undated last, then soonest first. Ordering on a bool behaves the same on
            // Postgres and the in-memory provider, unlike the string-persisted status enum.
            .OrderBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .MapToWorkspaceDto()
            .ToListAsync(ct);
    }

    public async Task<TaskResponseDto?> GetTaskByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await AccessibleTasks
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .Include(t => t.Updater)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return task?.MapToDto();
    }

    public async Task<TaskResponseDto?> CreateTaskAsync(
        Guid projectId,
        CreateTaskRequest dto,
        CancellationToken ct = default
    )
    {
        // (Guid?), not Guid: a projected value type comes back as Guid.Empty when there is
        // no row, which reads as a real id.
        var holdingWorkspaceId = await AccessibleProjects
            .Where(p => p.Id == projectId)
            .Select(p => (Guid?)p.WorkspaceId)
            .FirstOrDefaultAsync(ct);

        if (holdingWorkspaceId is not Guid workspaceId)
            return null;

        await RequireAssigneeIsMemberAsync(workspaceId, dto.AssigneeId, ct);

        var task = new TaskItem
        {
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status,
            Position = await NextPositionAsync(projectId, dto.Status, ct),
            ProjectId = projectId,
            AssigneeId = dto.AssigneeId,
            StartDate = dto.StartDate,
            DueDate = dto.DueDate,
            CompletedAt = dto.Status == TaskItemStatus.Done ? DateTime.UtcNow : null,
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(ct);

        return await LoadDtoAsync(task, ct);
    }

    public async Task<TaskResponseDto?> UpdateTaskAsync(
        Guid id,
        UpdateTaskRequest dto,
        CancellationToken ct = default
    )
    {
        var task = await AccessibleTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task is null)
            return null;

        // Only when it changes: an assignee who has since left the workspace would otherwise
        // make the task permanently uneditable, since nothing clears assignments on departure.
        if (dto.AssigneeId != task.AssigneeId)
        {
            var holdingWorkspaceId = await _context
                .Projects.Where(p => p.Id == task.ProjectId)
                .Select(p => (Guid?)p.WorkspaceId)
                .FirstOrDefaultAsync(ct);

            // Soft-deleted between the two reads.
            if (holdingWorkspaceId is not Guid workspaceId)
                return null;

            await RequireAssigneeIsMemberAsync(workspaceId, dto.AssigneeId, ct);
        }

        // A form carries no drop position, so a status change appends and closes the hole behind it.
        if (task.Status != dto.Status)
        {
            var sourceStatus = task.Status;
            task.Position = await NextPositionAsync(task.ProjectId, dto.Status, ct);
            task.Status = dto.Status;
            await RenumberColumnAsync(task.ProjectId, sourceStatus, task.Id, ct);
        }

        task.Title = dto.Title;
        task.Description = dto.Description;
        task.AssigneeId = dto.AssigneeId;
        task.StartDate = dto.StartDate;
        task.DueDate = dto.DueDate;
        StampCompletion(task);

        await _context.SaveChangesAsync(ct);

        return await LoadDtoAsync(task, ct);
    }

    public async Task<bool> DeleteTaskByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await AccessibleTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task is null)
            return false;

        // Not Remove(): it cascades to loaded dependents before the interceptor runs. The gap
        // this leaves in the column is harmless — Position is a sort key, never an index.
        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<IEnumerable<TaskResponseDto>?> GetProjectDeletedTasksAsync(
        Guid projectId,
        CancellationToken ct = default
    )
    {
        // A trashed project's task trash 404s: AccessibleProjects filters it out.
        if (!await AccessibleProjects.AnyAsync(p => p.Id == projectId, ct))
            return null;

        var cutoff = TrashCutoff;

        // No Include: MapToDto projects, and EF drops an Include on a projected query.
        return await _context
            .Tasks.IgnoreQueryFilters()
            .Where(t => t.ProjectId == projectId && t.IsDeleted && t.DeletedAt >= cutoff)
            .OrderByDescending(t => t.DeletedAt)
            .ThenBy(t => t.Id)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<WorkspaceTaskResponseDto>?> GetWorkspaceDeletedTasksAsync(
        Guid workspaceId,
        CancellationToken ct = default
    )
    {
        if (!await IsMemberAsync(workspaceId, ct))
            return null;

        var cutoff = TrashCutoff;

        // !t.Project.IsDeleted is spelled out because IgnoreQueryFilters() is query-wide: it
        // switches the Projects filter off along with the Tasks one. Without it a trashed
        // project's tasks would fill this list with rows RestoreTaskByIdAsync then refuses.
        // GetProjectDeletedTasksAsync needs no such clause; it validates its one project first.
        return await _context
            .Tasks.IgnoreQueryFilters()
            .Where(t =>
                t.Project!.WorkspaceId == workspaceId
                && !t.Project.IsDeleted
                && t.IsDeleted
                && t.DeletedAt >= cutoff
            )
            .OrderByDescending(t => t.DeletedAt)
            .ThenBy(t => t.Id)
            .MapToWorkspaceDto()
            .ToListAsync(ct);
    }

    public async Task<TaskResponseDto?> RestoreTaskByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var task = await _context
            .Tasks.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task is null)
            return null;

        // A separate, filtered query on purpose. IgnoreQueryFilters() above is query-wide in
        // EF, so composing the access check into it would switch off the Projects soft-delete
        // filter too — and restore a task into a project that is itself in the trash, where it
        // would sit invisible until someone restored the project and it silently reappeared.
        if (!await AccessibleProjects.AnyAsync(p => p.Id == task.ProjectId, ct))
            return null;

        if (task.IsDeleted)
        {
            // The window is policy, not decoration: a task the trash no longer lists must not
            // stay restorable by anyone who kept its id.
            if (task.DeletedAt < TrashCutoff)
                return null;

            task.IsDeleted = false;
            task.DeletedAt = null;
            task.Position = await RestoredPositionAsync(task, ct);
            await _context.SaveChangesAsync(ct);
        }

        return await LoadDtoAsync(task, ct);
    }

    /// <summary>
    /// Whether the caller can see this workspace at all. Both workspace-wide reads answer
    /// null rather than an empty list when this is false, so a workspace the caller cannot
    /// reach is a 404 and never "you have nothing here".
    /// </summary>
    private Task<bool> IsMemberAsync(Guid workspaceId, CancellationToken ct)
    {
        // Hoisted out of the expression tree: capturing `this` there is what makes EF
        // recompile the query per instance.
        var userId = _currentUser.UserGuid;

        return _context.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == workspaceId && m.UserId == userId,
            ct
        );
    }

    private DateTime TrashCutoff => DateTime.UtcNow.AddDays(-_trashWindowDays);

    /// <summary>
    /// Where a restored card lands. Its old position is usually still free — deleting
    /// deliberately leaves the gap — but a card created since will have taken it, because
    /// NextPositionAsync counts from the live maximum. Two cards on one position order by
    /// Id, which is arbitrary, so the late arrival goes to the end instead.
    /// </summary>
    private async Task<int> RestoredPositionAsync(TaskItem task, CancellationToken ct)
    {
        var taken = await _context.Tasks.AnyAsync(
            t =>
                t.ProjectId == task.ProjectId
                && t.Status == task.Status
                && t.Id != task.Id
                && t.Position == task.Position,
            ct
        );

        return taken ? await NextPositionAsync(task.ProjectId, task.Status, ct) : task.Position;
    }

    public async Task<TaskResponseDto?> MoveTaskAsync(
        Guid id,
        MoveTaskRequest dto,
        CancellationToken ct = default
    )
    {
        var task = await AccessibleTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (task is null)
            return null;

        var sourceStatus = task.Status;

        var column = await _context
            .Tasks.Where(t => t.ProjectId == task.ProjectId && t.Status == dto.Status && t.Id != id)
            .OrderBy(t => t.Position)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        column.Insert(ResolveDropIndex(column, dto.PreviousTaskId, dto.NextTaskId), task);

        task.Status = dto.Status;
        StampCompletion(task);

        Renumber(column, movedTask: task);

        if (sourceStatus != dto.Status)
            await RenumberColumnAsync(task.ProjectId, sourceStatus, task.Id, ct);

        await _context.SaveChangesAsync(ct);

        return await LoadDtoAsync(task, ct);
    }

    /// <summary>Where in the column the card was dropped.</summary>
    // A neighbour that has since moved or been deleted counts as absent, not as an error: the
    // card lands at whichever edge the surviving neighbour implies, or at the end if neither does.
    private static int ResolveDropIndex(
        List<TaskItem> column,
        Guid? previousTaskId,
        Guid? nextTaskId
    )
    {
        if (previousTaskId is Guid previous)
        {
            var index = column.FindIndex(t => t.Id == previous);
            if (index >= 0)
                return index + 1;
        }

        if (nextTaskId is Guid next)
        {
            var index = column.FindIndex(t => t.Id == next);
            if (index >= 0)
                return index;
        }

        return column.Count;
    }

    private async Task<int> NextPositionAsync(
        Guid projectId,
        TaskItemStatus status,
        CancellationToken ct
    )
    {
        // (int?) matters: Max over an empty column throws on a non-nullable projection.
        var last = await _context
            .Tasks.Where(t => t.ProjectId == projectId && t.Status == status)
            .MaxAsync(t => (int?)t.Position, ct);

        return (last ?? -1) + 1;
    }

    /// <summary>Closes the hole a task left behind when it moved out of this column.</summary>
    private async Task RenumberColumnAsync(
        Guid projectId,
        TaskItemStatus status,
        Guid departedId,
        CancellationToken ct
    )
    {
        var column = await _context
            .Tasks.Where(t => t.ProjectId == projectId && t.Status == status && t.Id != departedId)
            .OrderBy(t => t.Position)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        Renumber(column);
    }

    /// <summary>Renumbers a column 0..n-1, attributing the edit only to the card that moved.</summary>
    // Without the incidental mark, one drag leaves a whole column reading "updated by Bob,
    // just now"; without the skip, it writes every row instead of the ones that shifted.
    private void Renumber(List<TaskItem> column, TaskItem? movedTask = null)
    {
        for (var i = 0; i < column.Count; i++)
        {
            if (column[i].Position == i)
                continue;

            column[i].Position = i;

            if (column[i] != movedTask)
                _context.MarkAsIncidentalChange(column[i]);
        }
    }

    private static void StampCompletion(TaskItem task) =>
        task.CompletedAt =
            task.Status == TaskItemStatus.Done
                ? task.CompletedAt ?? DateTime.UtcNow // don't restamp a task that was already done
                : null;

    private async Task RequireAssigneeIsMemberAsync(
        Guid workspaceId,
        Guid? assigneeId,
        CancellationToken ct
    )
    {
        if (assigneeId is not Guid id)
            return;

        var isMember = await _context.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == workspaceId && m.UserId == id,
            ct
        );

        if (!isMember)
            throw new BusinessRuleException(
                BusinessRuleCodes.AssigneeNotWorkspaceMember,
                "That person is not a member of this workspace."
            );
    }

    private async Task<TaskResponseDto> LoadDtoAsync(TaskItem task, CancellationToken ct)
    {
        await _context.Entry(task).Reference(t => t.Assignee).LoadAsync(ct);
        await _context.Entry(task).Reference(t => t.Creator).LoadAsync(ct);
        await _context.Entry(task).Reference(t => t.Updater).LoadAsync(ct);

        return task.MapToDto();
    }
}
