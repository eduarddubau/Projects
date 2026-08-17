using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services;

public sealed class DashboardServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly DashboardService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    private readonly Workspace _mine;
    private readonly Workspace _alsoMine;
    private readonly Workspace _theirs;

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);
        _service = new DashboardService(_context, _currentUser.Object);

        _currentUser.Setup(c => c.UserGuid).Returns(_userId);

        _mine = AddWorkspace("Mine", _userId);
        _alsoMine = AddWorkspace("Also mine", _userId);
        _theirs = AddWorkspace("Theirs", _otherUserId);
    }

    private Workspace AddWorkspace(string name, Guid memberId)
    {
        var workspace = new Workspace
        {
            Name = name,
            Members =
            {
                new WorkspaceMember
                {
                    UserId = memberId,
                    Role = WorkspaceRole.Owner,
                    JoinedAt = DateTime.UtcNow,
                },
            },
        };
        _context.Workspaces.Add(workspace);
        _context.SaveChanges();
        return workspace;
    }

    // Audit stamping lives in SaveChangesAsync only, so the sync save here
    // persists these timestamps untouched.
    private Project AddProject(string name, Workspace workspace, DateTime? deletedAt = null)
    {
        var project = new Project
        {
            Name = name,
            CreatedBy = _userId,
            WorkspaceId = workspace.Id,
            IsDeleted = deletedAt is not null,
            DeletedAt = deletedAt,
        };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    private void AddTask(string title, Project project, Guid? assigneeId, TaskItemStatus status)
    {
        _context.Tasks.Add(
            new TaskItem
            {
                Title = title,
                ProjectId = project.Id,
                AssigneeId = assigneeId,
                Status = status,
            }
        );
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetWorkspaceDashboardAsync_CountsOnlyTheWorkspaceAskedFor()
    {
        AddTask("Here", AddProject("One", _mine), _userId, TaskItemStatus.Todo);
        AddTask("Also here", AddProject("Two", _mine), _userId, TaskItemStatus.Todo);
        AddTask(
            "Elsewhere but still mine",
            AddProject("Other", _alsoMine),
            _userId,
            TaskItemStatus.Todo
        );

        var result = await _service.GetWorkspaceDashboardAsync(
            _mine.Id,
            TestContext.Current.CancellationToken
        );

        // Two, not three: the other workspace is the caller's too, and still must not count.
        Assert.Equal(2, result!.OpenTaskCount);
        Assert.Equal(2, result.MyOpenTaskCount);
    }

    [Fact]
    public async Task GetWorkspaceDashboardAsync_ExcludesTasksOfTrashedProjects()
    {
        var live = AddProject("Live", _mine);
        var trashed = AddProject("Trashed", _mine, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddTask("Mine, open", live, _userId, TaskItemStatus.Todo);
        AddTask("Mine, but the project is in the trash", trashed, _userId, TaskItemStatus.Todo);

        var result = await _service.GetWorkspaceDashboardAsync(
            _mine.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, result!.OpenTaskCount);
        Assert.Equal(1, result.MyOpenTaskCount);
    }

    [Fact]
    public async Task GetWorkspaceDashboardAsync_CountsUnfinishedTasksAndMyShareOfThem()
    {
        var project = AddProject("Project", _mine);
        AddTask("Mine, todo", project, _userId, TaskItemStatus.Todo);
        AddTask("Mine, in progress", project, _userId, TaskItemStatus.InProgress);
        AddTask("Mine, done", project, _userId, TaskItemStatus.Done);
        AddTask("Theirs, todo", project, _otherUserId, TaskItemStatus.Todo);
        AddTask("Theirs, done", project, _otherUserId, TaskItemStatus.Done);
        AddTask("Nobody's, todo", project, null, TaskItemStatus.Todo);

        var result = await _service.GetWorkspaceDashboardAsync(
            _mine.Id,
            TestContext.Current.CancellationToken
        );

        // Four open of six, and an unassigned one counts toward the workspace but not me.
        Assert.Equal(4, result!.OpenTaskCount);
        Assert.Equal(2, result.MyOpenTaskCount);
    }

    [Fact]
    public async Task GetWorkspaceDashboardAsync_WhenNotAMember_ReturnsNull()
    {
        AddTask("Not for me", AddProject("Not for me", _theirs), _otherUserId, TaskItemStatus.Todo);

        var result = await _service.GetWorkspaceDashboardAsync(
            _theirs.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorkspaceDashboardAsync_WhenEmpty_ReturnsZeroes()
    {
        var result = await _service.GetWorkspaceDashboardAsync(
            _mine.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, result!.OpenTaskCount);
        Assert.Equal(0, result.MyOpenTaskCount);
    }

    public void Dispose() => _context.Dispose();
}
