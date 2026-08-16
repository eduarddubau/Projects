using Backend.Data;
using Backend.DTOs.Task;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
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

        _service = new TaskService(_context, _currentUser.Object);

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
        Project? project = null
    )
    {
        var task = new TaskItem
        {
            Title = title,
            Status = status,
            Position = position,
            ProjectId = (project ?? _project).Id,
        };
        _context.Tasks.Add(task);
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

    public void Dispose() => _context.Dispose();
}
