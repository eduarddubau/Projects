using Backend.Config;
using Backend.Data;
using Backend.DTOs.Task;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.Tests.Services;

public sealed class TaskServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly TaskService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();
    private const int TrashWindowDays = 30;

    // Member of the shared workspace, absent from the foreign one.
    private readonly Workspace _shared;
    private readonly Workspace _foreign;
    private readonly Project _project;
    private readonly Project _foreignProject;

    public TaskServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);
        _currentUser.Setup(c => c.UserGuid).Returns(_userId);

        _service = new TaskService(
            _context,
            _currentUser.Object,
            Options.Create(new RetentionOptions { TrashWindowDays = TrashWindowDays })
        );

        _shared = AddWorkspace(
            "Shared",
            (_otherUserId, WorkspaceRole.Owner),
            (_userId, WorkspaceRole.Member)
        );
        _foreign = AddWorkspace("Foreign", (_otherUserId, WorkspaceRole.Owner));

        _project = AddProject("Reachable", _shared);
        _foreignProject = AddProject("Unreachable", _foreign);
    }

    private Workspace AddWorkspace(string name, params (Guid UserId, WorkspaceRole Role)[] members)
    {
        var workspace = new Workspace { Name = name, IsPersonal = false };

        foreach (var (userId, role) in members)
            workspace.Members.Add(
                new WorkspaceMember
                {
                    UserId = userId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                }
            );

        _context.Workspaces.Add(workspace);
        _context.SaveChanges();
        return workspace;
    }

    private Project AddProject(string name, Workspace workspace)
    {
        var project = new Project
        {
            Name = name,
            CreatedBy = _userId,
            WorkspaceId = workspace.Id,
        };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    private TaskItem AddTask(
        string title,
        TaskItemStatus status = TaskItemStatus.Todo,
        int position = 0,
        Project? project = null,
        Guid? assigneeId = null,
        DateOnly? dueDate = null,
        bool isDeleted = false,
        DateTime? deletedAt = null
    )
    {
        var task = new TaskItem
        {
            Title = title,
            Status = status,
            Position = position,
            ProjectId = (project ?? _project).Id,
            AssigneeId = assigneeId,
            DueDate = dueDate,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
        };
        _context.Tasks.Add(task);
        // Synchronous on purpose: only SaveChangesAsync carries the audit interceptor, which
        // forces IsDeleted back to false on an Added row. This is how a test seeds one deleted.
        _context.SaveChanges();
        return task;
    }

    private static CreateTaskRequest Create(
        string title,
        TaskItemStatus status = TaskItemStatus.Todo,
        Guid? assigneeId = null,
        DateOnly? startDate = null,
        DateOnly? dueDate = null
    ) => new(title, null, status, assigneeId, startDate, dueDate);

    private List<string> ColumnTitles(TaskItemStatus status) =>
        [
            .. _context
                .Tasks.Where(t => t.ProjectId == _project.Id && t.Status == status)
                .OrderBy(t => t.Position)
                .ThenBy(t => t.Id)
                .Select(t => t.Title),
        ];

    private List<int> ColumnPositions(TaskItemStatus status) =>
        [
            .. _context
                .Tasks.Where(t => t.ProjectId == _project.Id && t.Status == status)
                .OrderBy(t => t.Position)
                .Select(t => t.Position),
        ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Access scoping

    [Fact]
    public async Task GetProjectTasksAsync_ReturnsNullForAProjectTheCallerCannotReach()
    {
        AddTask("Hidden", project: _foreignProject);

        Assert.Null(await _service.GetProjectTasksAsync(_foreignProject.Id, Ct));
    }

    [Fact]
    public async Task GetTaskByIdAsync_ReturnsNullForAForeignProjectsTask()
    {
        var task = AddTask("Hidden", project: _foreignProject);

        Assert.Null(await _service.GetTaskByIdAsync(task.Id, Ct));
    }

    [Fact]
    public async Task UpdateTaskAsync_ReturnsNullForAForeignProjectsTask()
    {
        var task = AddTask("Hidden", project: _foreignProject);

        var result = await _service.UpdateTaskAsync(
            task.Id,
            new UpdateTaskRequest("Renamed", null, TaskItemStatus.Done, null, null, null),
            Ct
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteTaskByIdAsync_ReturnsFalseForAForeignProjectsTask()
    {
        var task = AddTask("Hidden", project: _foreignProject);

        Assert.False(await _service.DeleteTaskByIdAsync(task.Id, Ct));
    }

    [Fact]
    public async Task MoveTaskAsync_ReturnsNullForAForeignProjectsTask()
    {
        var task = AddTask("Hidden", project: _foreignProject);

        var result = await _service.MoveTaskAsync(
            task.Id,
            new MoveTaskRequest(TaskItemStatus.Done, null, null),
            Ct
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateTaskAsync_ReturnsNullForAProjectTheCallerCannotReach()
    {
        Assert.Null(await _service.CreateTaskAsync(_foreignProject.Id, Create("Nope"), Ct));
    }

    // The cascade contract: nothing is written when a project is trashed.

    [Fact]
    public async Task TasksOfASoftDeletedProject_AreUnreachableThenReturnOnRestore()
    {
        var task = AddTask("Survives");

        _project.IsDeleted = true;
        _project.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(Ct);

        Assert.Null(await _service.GetTaskByIdAsync(task.Id, Ct));
        Assert.Null(await _service.GetProjectTasksAsync(_project.Id, Ct));

        // The row was never touched, which is what makes restore exact.
        Assert.False(_context.Tasks.IgnoreQueryFilters().Single(t => t.Id == task.Id).IsDeleted);

        _project.IsDeleted = false;
        _project.DeletedAt = null;
        await _context.SaveChangesAsync(Ct);

        Assert.NotNull(await _service.GetTaskByIdAsync(task.Id, Ct));
    }

    // Assignment

    [Fact]
    public async Task CreateTaskAsync_RejectsAnAssigneeWhoIsNotAWorkspaceMember()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateTaskAsync(_project.Id, Create("Nope", assigneeId: _strangerId), Ct)
        );

        Assert.Equal("AssigneeNotWorkspaceMember", exception.Code);
    }

    [Fact]
    public async Task UpdateTaskAsync_RejectsAnAssigneeWhoIsNotAWorkspaceMember()
    {
        var task = AddTask("Existing");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateTaskAsync(
                task.Id,
                new UpdateTaskRequest(
                    "Existing",
                    null,
                    TaskItemStatus.Todo,
                    _strangerId,
                    null,
                    null
                ),
                Ct
            )
        );
    }

    [Fact]
    public async Task CreateTaskAsync_AcceptsAnAssigneeWhoIsAWorkspaceMember()
    {
        var result = await _service.CreateTaskAsync(
            _project.Id,
            Create("Assigned", assigneeId: _otherUserId),
            Ct
        );

        Assert.Equal(_otherUserId, result!.AssigneeId);
    }

    // Ordering

    [Fact]
    public async Task CreateTaskAsync_AppendsToTheEndOfItsOwnColumn()
    {
        AddTask("First", TaskItemStatus.Todo, 0);
        AddTask("Only in progress", TaskItemStatus.InProgress, 0);

        var created = await _service.CreateTaskAsync(_project.Id, Create("Second"), Ct);

        Assert.Equal(1, created!.Position);
        Assert.Equal(["First", "Second"], ColumnTitles(TaskItemStatus.Todo));
    }

    [Fact]
    public async Task MoveTaskAsync_BetweenTwoCards_LandsStrictlyBetweenThem()
    {
        var first = AddTask("First", TaskItemStatus.Todo, 0);
        var second = AddTask("Second", TaskItemStatus.Todo, 1);
        var mover = AddTask("Mover", TaskItemStatus.Todo, 2);

        await _service.MoveTaskAsync(
            mover.Id,
            new MoveTaskRequest(TaskItemStatus.Todo, first.Id, second.Id),
            Ct
        );

        Assert.Equal(["First", "Mover", "Second"], ColumnTitles(TaskItemStatus.Todo));
        Assert.Equal([0, 1, 2], ColumnPositions(TaskItemStatus.Todo));
    }

    [Fact]
    public async Task MoveTaskAsync_ToTheHead_UsesTheNextNeighbourAlone()
    {
        var first = AddTask("First", TaskItemStatus.Todo, 0);
        var mover = AddTask("Mover", TaskItemStatus.Todo, 1);

        await _service.MoveTaskAsync(
            mover.Id,
            new MoveTaskRequest(TaskItemStatus.Todo, null, first.Id),
            Ct
        );

        Assert.Equal(["Mover", "First"], ColumnTitles(TaskItemStatus.Todo));
    }

    [Fact]
    public async Task MoveTaskAsync_AcrossColumns_RenumbersBoth()
    {
        AddTask("Stays first", TaskItemStatus.Todo, 0);
        var mover = AddTask("Mover", TaskItemStatus.Todo, 1);
        AddTask("Stays last", TaskItemStatus.Todo, 2);
        AddTask("Already there", TaskItemStatus.InProgress, 0);

        await _service.MoveTaskAsync(
            mover.Id,
            new MoveTaskRequest(TaskItemStatus.InProgress, null, null),
            Ct
        );

        // The hole the mover left behind is closed, not left as 0, 2.
        Assert.Equal(["Stays first", "Stays last"], ColumnTitles(TaskItemStatus.Todo));
        Assert.Equal([0, 1], ColumnPositions(TaskItemStatus.Todo));
        Assert.Equal(["Already there", "Mover"], ColumnTitles(TaskItemStatus.InProgress));
        Assert.Equal([0, 1], ColumnPositions(TaskItemStatus.InProgress));
    }

    [Fact]
    public async Task MoveTaskAsync_WithAStaleNeighbour_FallsBackToTheColumnEndInsteadOfThrowing()
    {
        AddTask("Present", TaskItemStatus.Todo, 0);
        var mover = AddTask("Mover", TaskItemStatus.InProgress, 0);

        var result = await _service.MoveTaskAsync(
            mover.Id,
            new MoveTaskRequest(TaskItemStatus.Todo, Guid.NewGuid(), Guid.NewGuid()),
            Ct
        );

        Assert.NotNull(result);
        Assert.Equal(["Present", "Mover"], ColumnTitles(TaskItemStatus.Todo));
    }

    [Fact]
    public async Task DeleteTaskByIdAsync_SoftDeletesAndLeavesTheRow()
    {
        var task = AddTask("Doomed");

        Assert.True(await _service.DeleteTaskByIdAsync(task.Id, Ct));

        var stored = _context.Tasks.IgnoreQueryFilters().Single(t => t.Id == task.Id);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Null(await _service.GetTaskByIdAsync(task.Id, Ct));
    }

    // Task trash and restore. The caller is a plain Member of _shared throughout, which is
    // the point: deleting stays member-open because it is recoverable by the same member.

    [Fact]
    public async Task GetProjectDeletedTasksAsync_ReturnsDeletedTasksWithinRetentionWindow()
    {
        AddTask("Live");
        AddTask("RecentlyDeleted", isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddTask(
            "OldDeleted",
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );

        var result = await _service.GetProjectDeletedTasksAsync(_project.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(["RecentlyDeleted"], result.Select(t => t.Title));
    }

    [Fact]
    public async Task GetProjectDeletedTasksAsync_ForAForeignProject_ReturnsNull()
    {
        AddTask("Theirs", project: _foreignProject, isDeleted: true, deletedAt: DateTime.UtcNow);

        Assert.Null(await _service.GetProjectDeletedTasksAsync(_foreignProject.Id, Ct));
    }

    // A trashed project takes its task trash with it, rather than offering restores back
    // into a project that is not there. Falls out of AccessibleProjects, but pin it.
    [Fact]
    public async Task GetProjectDeletedTasksAsync_WhenTheProjectItselfIsDeleted_ReturnsNull()
    {
        AddTask("Doomed", isDeleted: true, deletedAt: DateTime.UtcNow);
        _project.IsDeleted = true;
        _project.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(Ct);

        Assert.Null(await _service.GetProjectDeletedTasksAsync(_project.Id, Ct));
    }

    [Fact]
    public async Task GetWorkspaceDeletedTasksAsync_GathersTrashFromEveryProjectInTheWorkspace()
    {
        var second = AddProject("Second", _shared);
        AddTask("Live");
        AddTask("FromFirst", isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-2));
        AddTask("FromSecond", project: second, isDeleted: true, deletedAt: DateTime.UtcNow);

        var result = await _service.GetWorkspaceDeletedTasksAsync(_shared.Id, Ct);

        Assert.NotNull(result);
        // Newest first, and each row says which project it came from — the whole reason the
        // workspace trash projects to WorkspaceTaskResponseDto rather than TaskResponseDto.
        Assert.Equal(["FromSecond", "FromFirst"], result.Select(t => t.Title));
        Assert.Equal(["Second", "Reachable"], result.Select(t => t.ProjectName));
    }

    // The IgnoreQueryFilters trap, pinned: that call is query-wide, so dropping the explicit
    // !t.Project.IsDeleted turns the Projects filter off too and this list fills with rows
    // RestoreTaskByIdAsync refuses. Delete the clause and this test must go red.
    [Fact]
    public async Task GetWorkspaceDeletedTasksAsync_LeavesOutTasksOfATrashedProject()
    {
        var doomed = AddProject("Doomed", _shared);
        AddTask("Survivor", isDeleted: true, deletedAt: DateTime.UtcNow);
        AddTask("Buried", project: doomed, isDeleted: true, deletedAt: DateTime.UtcNow);

        doomed.IsDeleted = true;
        doomed.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(Ct);

        var result = await _service.GetWorkspaceDeletedTasksAsync(_shared.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(["Survivor"], result.Select(t => t.Title));
    }

    [Fact]
    public async Task GetWorkspaceDeletedTasksAsync_LeavesOutLiveTasksAndOnesPastTheWindow()
    {
        AddTask("Live");
        AddTask("Recent", isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddTask(
            "Expired",
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );

        var result = await _service.GetWorkspaceDeletedTasksAsync(_shared.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(["Recent"], result.Select(t => t.Title));
    }

    [Fact]
    public async Task GetWorkspaceDeletedTasksAsync_ForAWorkspaceTheCallerIsNotIn_ReturnsNull()
    {
        AddTask("Theirs", project: _foreignProject, isDeleted: true, deletedAt: DateTime.UtcNow);

        Assert.Null(await _service.GetWorkspaceDeletedTasksAsync(_foreign.Id, Ct));
    }

    // A member, not an owner: the workspace task trash is member-visible on purpose, because
    // deleting a task is member-open and recovery has to reach whoever did the deleting.
    [Fact]
    public async Task GetWorkspaceDeletedTasksAsync_AnswersAPlainMember()
    {
        AddTask("Mine", isDeleted: true, deletedAt: DateTime.UtcNow);

        var result = await _service.GetWorkspaceDeletedTasksAsync(_shared.Id, Ct);

        Assert.Equal(
            WorkspaceRole.Member,
            _context.WorkspaceMembers.Single(m =>
                m.WorkspaceId == _shared.Id && m.UserId == _userId
            ).Role
        );
        Assert.Equal(["Mine"], result!.Select(t => t.Title));
    }

    [Fact]
    public async Task RestoreTaskByIdAsync_BringsTheTaskBackToItsBoard()
    {
        var task = AddTask("Back", isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));

        var restored = await _service.RestoreTaskByIdAsync(task.Id, Ct);

        Assert.NotNull(restored);
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.NotNull(await _service.GetTaskByIdAsync(task.Id, Ct));
        Assert.Empty((await _service.GetProjectDeletedTasksAsync(_project.Id, Ct))!);
    }

    // Position is a sort key, never an index, so the gap the delete left is where it lands.
    [Fact]
    public async Task RestoreTaskByIdAsync_PutsTheCardBackInItsOldColumnPosition()
    {
        AddTask("First", position: 0);
        var removed = AddTask("Second", position: 1);
        AddTask("Third", position: 2);

        Assert.True(await _service.DeleteTaskByIdAsync(removed.Id, Ct));
        Assert.Equal(["First", "Third"], ColumnTitles(TaskItemStatus.Todo));

        await _service.RestoreTaskByIdAsync(removed.Id, Ct);

        Assert.Equal(["First", "Second", "Third"], ColumnTitles(TaskItemStatus.Todo));
    }

    [Fact]
    public async Task RestoreTaskByIdAsync_ForAForeignProjectsTask_ReturnsNull()
    {
        var task = AddTask(
            "Theirs",
            project: _foreignProject,
            isDeleted: true,
            deletedAt: DateTime.UtcNow
        );

        Assert.Null(await _service.RestoreTaskByIdAsync(task.Id, Ct));
    }

    // Restoring something already live is a no-op, not an error: two people can hit Restore
    // on the same row from a list neither has refreshed.
    [Fact]
    public async Task RestoreTaskByIdAsync_OnALiveTask_ReturnsItUnchanged()
    {
        var task = AddTask("Live");

        var restored = await _service.RestoreTaskByIdAsync(task.Id, Ct);

        Assert.NotNull(restored);
        Assert.False(restored.IsDeleted);
    }

    // IgnoreQueryFilters() is query-wide in EF, so composing the access check into the same
    // query switched off the Projects filter and let this through. It would have left a live
    // task under a trashed project, invisible until the project came back.
    [Fact]
    public async Task RestoreTaskByIdAsync_WhenTheProjectIsItselfDeleted_ReturnsNull()
    {
        var task = AddTask("Orphan", isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));
        _project.IsDeleted = true;
        _project.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(Ct);

        Assert.Null(await _service.RestoreTaskByIdAsync(task.Id, Ct));

        var stored = _context.Tasks.IgnoreQueryFilters().Single(t => t.Id == task.Id);
        Assert.True(stored.IsDeleted);
    }

    // The retention window is policy, not a display filter: a row the trash stopped listing
    // must not stay restorable by anyone still holding its id.
    [Fact]
    public async Task RestoreTaskByIdAsync_PastTheRetentionWindow_ReturnsNull()
    {
        var task = AddTask(
            "Ancient",
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );

        Assert.Null(await _service.RestoreTaskByIdAsync(task.Id, Ct));
    }

    // Deleting leaves the gap, but creating counts from the live maximum and fills it. Two
    // cards on one position order by Id, which is arbitrary, so the late arrival goes last.
    [Fact]
    public async Task RestoreTaskByIdAsync_WhenSomethingTookItsPosition_GoesToTheEnd()
    {
        AddTask("First", position: 0);
        var removed = AddTask("Second", position: 1);

        Assert.True(await _service.DeleteTaskByIdAsync(removed.Id, Ct));
        var created = await _service.CreateTaskAsync(_project.Id, Create("Third"), Ct);
        Assert.Equal(removed.Position, created!.Position);

        await _service.RestoreTaskByIdAsync(removed.Id, Ct);

        Assert.Equal(["First", "Third", "Second"], ColumnTitles(TaskItemStatus.Todo));
    }

    // Completion stamping

    [Fact]
    public async Task MoveTaskAsync_ToDone_StampsCompletedAt_AndClearsItOnTheWayBack()
    {
        var task = AddTask("Work");

        var done = await _service.MoveTaskAsync(
            task.Id,
            new MoveTaskRequest(TaskItemStatus.Done, null, null),
            Ct
        );
        Assert.NotNull(done!.CompletedAt);

        var reopened = await _service.MoveTaskAsync(
            task.Id,
            new MoveTaskRequest(TaskItemStatus.Todo, null, null),
            Ct
        );
        Assert.Null(reopened!.CompletedAt);
    }

    [Fact]
    public async Task MoveTaskAsync_WithinDone_DoesNotRestampCompletedAt()
    {
        var first = AddTask("First", TaskItemStatus.Done, 0);
        var mover = AddTask("Mover", TaskItemStatus.Done, 1);

        var stamped = DateTime.UtcNow.AddDays(-3);
        mover.CompletedAt = stamped;
        await _context.SaveChangesAsync(Ct);

        var result = await _service.MoveTaskAsync(
            mover.Id,
            new MoveTaskRequest(TaskItemStatus.Done, null, first.Id),
            Ct
        );

        Assert.Equal(stamped, result!.CompletedAt);
    }

    [Fact]
    public async Task UpdateTaskAsync_ChangingStatus_MovesToTheEndOfTheNewColumnAndClosesTheHole()
    {
        AddTask("Stays", TaskItemStatus.Todo, 0);
        var mover = AddTask("Mover", TaskItemStatus.Todo, 1);
        AddTask("Also stays", TaskItemStatus.Todo, 2);
        AddTask("Already there", TaskItemStatus.Done, 0);

        await _service.UpdateTaskAsync(
            mover.Id,
            new UpdateTaskRequest("Mover", null, TaskItemStatus.Done, null, null, null),
            Ct
        );

        Assert.Equal(["Stays", "Also stays"], ColumnTitles(TaskItemStatus.Todo));
        Assert.Equal([0, 1], ColumnPositions(TaskItemStatus.Todo));
        Assert.Equal(["Already there", "Mover"], ColumnTitles(TaskItemStatus.Done));
    }

    // Pins workflow order — Todo, InProgress, Done — against the alphabetical order the
    // string-persisted enum would otherwise give in Postgres (Done, InProgress, Todo).
    // Weaker than it looks: the in-memory provider sorts enums by value, so this test passed
    // while the live API disagreed. Only the ordering expression makes both agree.
    [Fact]
    public async Task GetProjectTasksAsync_OrdersByWorkflowStatusThenPosition()
    {
        AddTask("Todo second", TaskItemStatus.Todo, 1);
        AddTask("Todo first", TaskItemStatus.Todo, 0);
        AddTask("Doing", TaskItemStatus.InProgress, 0);
        AddTask("Done", TaskItemStatus.Done, 0);

        var result = await _service.GetProjectTasksAsync(_project.Id, Ct);

        Assert.Equal(["Todo first", "Todo second", "Doing", "Done"], result!.Select(t => t.Title));
    }

    [Fact]
    public async Task UpdateTaskAsync_KeepsAnAssigneeWhoHasSinceLeftTheWorkspace()
    {
        var task = AddTask("Assigned to a leaver");
        task.AssigneeId = _otherUserId;
        await _context.SaveChangesAsync(Ct);

        _context.WorkspaceMembers.Remove(
            _context.WorkspaceMembers.Single(m =>
                m.WorkspaceId == _shared.Id && m.UserId == _otherUserId
            )
        );
        await _context.SaveChangesAsync(Ct);

        // Re-validating an unchanged assignee would make the task permanently uneditable.
        var result = await _service.UpdateTaskAsync(
            task.Id,
            new UpdateTaskRequest("Renamed", null, TaskItemStatus.Todo, _otherUserId, null, null),
            Ct
        );

        Assert.Equal("Renamed", result!.Title);
    }

    [Fact]
    public async Task MoveTaskAsync_DoesNotAttributeTheShiftToNeighbouringCards()
    {
        var neighbour = AddTask("Neighbour", TaskItemStatus.Todo, 0);
        var mover = AddTask("Mover", TaskItemStatus.Todo, 1);

        await _service.MoveTaskAsync(
            mover.Id,
            new MoveTaskRequest(TaskItemStatus.Todo, null, neighbour.Id),
            Ct
        );

        // The neighbour shifted from 0 to 1, but nobody edited it.
        var shifted = _context.Tasks.Single(t => t.Id == neighbour.Id);
        Assert.Equal(1, shifted.Position);
        Assert.Null(shifted.UpdatedAt);
        Assert.NotNull(_context.Tasks.Single(t => t.Id == mover.Id).UpdatedAt);
    }

    [Fact]
    public async Task CreateTaskAsync_RoundTripsDatesAsCalendarDays()
    {
        var start = new DateOnly(2026, 8, 20);
        var due = new DateOnly(2026, 8, 25);

        var created = await _service.CreateTaskAsync(
            _project.Id,
            Create("Dated", startDate: start, dueDate: due),
            Ct
        );

        Assert.Equal(start, created!.StartDate);
        Assert.Equal(due, created.DueDate);
    }

    // Workspace task list

    private async Task<List<WorkspaceTaskResponseDto>> WorkspaceTasks(
        TaskAssigneeFilter assignee = TaskAssigneeFilter.Any,
        DateOnly? dueBefore = null
    )
    {
        var result = await _service.GetWorkspaceTasksAsync(
            _shared.Id,
            new WorkspaceTaskQuery { Assignee = assignee, DueBefore = dueBefore },
            Ct
        );
        return [.. result!];
    }

    [Fact]
    public async Task GetWorkspaceTasksAsync_ReturnsNullForAWorkspaceTheCallerIsNotIn()
    {
        AddTask("Hidden", project: _foreignProject);

        Assert.Null(
            await _service.GetWorkspaceTasksAsync(_foreign.Id, new WorkspaceTaskQuery(), Ct)
        );
    }

    // The board is where Done lives; this list is work still to do.
    [Fact]
    public async Task GetWorkspaceTasksAsync_LeavesOutDoneTasks()
    {
        AddTask("Open");
        AddTask("Finished", TaskItemStatus.Done);

        Assert.Equal(["Open"], (await WorkspaceTasks()).Select(t => t.Title));
    }

    [Fact]
    public async Task GetWorkspaceTasksAsync_LeavesOutTasksOfATrashedProject()
    {
        var doomed = AddProject("Doomed", _shared);
        AddTask("Kept");
        AddTask("Goes with the project", project: doomed);

        doomed.IsDeleted = true;
        doomed.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(Ct);

        Assert.Equal(["Kept"], (await WorkspaceTasks()).Select(t => t.Title));
    }

    [Fact]
    public async Task GetWorkspaceTasksAsync_FiltersToTheCallersOwnTasks()
    {
        AddTask("Mine", assigneeId: _userId);
        AddTask("Theirs", assigneeId: _otherUserId);
        AddTask("Nobody's");

        Assert.Equal(["Mine"], (await WorkspaceTasks(TaskAssigneeFilter.Me)).Select(t => t.Title));
    }

    [Fact]
    public async Task GetWorkspaceTasksAsync_FiltersToUnassignedTasks()
    {
        AddTask("Mine", assigneeId: _userId);
        AddTask("Nobody's");

        Assert.Equal(
            ["Nobody's"],
            (await WorkspaceTasks(TaskAssigneeFilter.Unassigned)).Select(t => t.Title)
        );
    }

    // An undated task is not overdue, which a naive "DueDate < today" on a null would get wrong.
    // The cutoff is the caller's day, so the assertion does not depend on the test host's clock.
    [Fact]
    public async Task GetWorkspaceTasksAsync_FiltersToDatesBeforeTheCallersDay()
    {
        var today = new DateOnly(2026, 8, 20);
        AddTask("Late", dueDate: today.AddDays(-1));
        AddTask("Due today", dueDate: today);
        AddTask("Later", dueDate: today.AddDays(3));
        AddTask("Undated");

        Assert.Equal(["Late"], (await WorkspaceTasks(dueBefore: today)).Select(t => t.Title));
    }

    [Fact]
    public async Task GetWorkspaceTasksAsync_OrdersBySoonestDueWithUndatedLast()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        AddTask("Undated");
        AddTask("Later", dueDate: today.AddDays(5));
        AddTask("Soonest", dueDate: today.AddDays(1));

        Assert.Equal(
            ["Soonest", "Later", "Undated"],
            (await WorkspaceTasks()).Select(t => t.Title)
        );
    }

    // The row is read away from its board, so it has to say which project it belongs to.
    [Fact]
    public async Task GetWorkspaceTasksAsync_NamesTheProjectEachTaskIsIn()
    {
        AddTask("Somewhere");

        Assert.Equal(_project.Name, (await WorkspaceTasks()).Single().ProjectName);
    }

    public void Dispose() => _context.Dispose();
}
