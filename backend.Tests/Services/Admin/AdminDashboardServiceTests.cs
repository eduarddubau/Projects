using Backend.Config;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Services.Admin;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Backend.Tests.Services.Admin;

public sealed class AdminDashboardServiceTests : IDisposable
{
    // The window the purge enforces. Listing still ignores it — the admin trash shows a
    // deleted project at any age — but PurgeableProjectCount counts against this.
    private const int TrashWindowDays = 30;

    private const string EnvironmentName = "Testing";

    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly AdminDashboardService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    private readonly Workspace _mine;
    private readonly Workspace _theirs;

    public AdminDashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(EnvironmentName);

        _service = new AdminDashboardService(
            _context,
            new TrashWindow { Days = TrashWindowDays },
            environment.Object
        );

        _currentUser.Setup(c => c.UserGuid).Returns(_userId);

        _mine = AddWorkspace("Mine", _userId);
        _theirs = AddWorkspace("Theirs", _otherUserId);
    }

    private Workspace AddWorkspace(
        string name,
        Guid memberId,
        DateTime? deletedAt = null,
        bool isPersonal = false
    )
    {
        var workspace = new Workspace
        {
            Name = name,
            IsPersonal = isPersonal,
            IsDeleted = deletedAt is not null,
            DeletedAt = deletedAt,
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

    private User AddUser(
        string email,
        bool isDeleted = false,
        bool isAnonymized = false,
        DateTime? createdAt = null,
        DateTimeOffset? lockoutEnd = null,
        bool lockoutEnabled = true
    )
    {
        var user = new User
        {
            Email = email,
            UserName = email,
            FirstName = "Alan",
            LastName = "Turing",
            IsDeleted = isDeleted,
            IsAnonymized = isAnonymized,
            CreatedAt = createdAt ?? default,
            LockoutEnd = lockoutEnd,
            LockoutEnabled = lockoutEnabled,
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    // Audit stamping lives in SaveChangesAsync only, so the sync save here
    // persists these timestamps untouched.
    private Project AddProject(
        string name,
        Workspace workspace,
        Guid? createdBy = null,
        DateTime? deletedAt = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null
    )
    {
        var project = new Project
        {
            Name = name,
            CreatedBy = createdBy ?? _userId,
            WorkspaceId = workspace.Id,
            IsDeleted = deletedAt is not null,
            DeletedAt = deletedAt,
            CreatedAt = createdAt ?? default,
            UpdatedAt = updatedAt,
        };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    private void AddTask(string title, Project project, DateTime? deletedAt = null)
    {
        _context.Tasks.Add(
            new TaskItem
            {
                Title = title,
                ProjectId = project.Id,
                Status = TaskItemStatus.Todo,
                IsDeleted = deletedAt is not null,
                DeletedAt = deletedAt,
            }
        );
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CountsAcrossAllUsers()
    {
        var owner = AddUser("owner@example.com", createdAt: DateTime.UtcNow.AddDays(-3));
        AddUser("deleted@example.com", isDeleted: true, createdAt: DateTime.UtcNow.AddDays(-2));
        AddUser(
            "erased@example.com",
            isDeleted: true,
            isAnonymized: true,
            createdAt: DateTime.UtcNow.AddDays(-1)
        );

        AddProject("Mine", _mine, createdAt: DateTime.UtcNow.AddDays(-2));
        AddProject("Theirs", _theirs, createdBy: owner.Id, createdAt: DateTime.UtcNow.AddDays(-1));
        // The admin trash has no window, so even old deletions stay counted as deleted.
        AddProject(
            "Old deleted",
            _theirs,
            createdBy: owner.Id,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 10))
        );

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ActiveProjectCount);
        Assert.Equal(1, result.DeletedProjectCount);
        Assert.Equal(1, result.ActiveUserCount);
        Assert.Equal(1, result.DeletedUserCount);
        Assert.Equal(["owner@example.com"], result.RecentUsers.Select(u => u.Email));
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CapsRecentUsersAtFive()
    {
        for (var i = 0; i < 7; i++)
        {
            AddUser($"user{i}@example.com", createdAt: DateTime.UtcNow.AddDays(-i));
            AddProject($"Project {i}", _mine, createdAt: DateTime.UtcNow.AddDays(-i));
        }

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, result.RecentUsers.Count);
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CountsLiveWorkspacesAndTasks()
    {
        // The two from the fixture are live and shared; this third one is trashed.
        AddWorkspace("Gone", _userId, deletedAt: DateTime.UtcNow.AddDays(-1));
        // Every account holds one of these and cannot delete it, so counting them would
        // just restate the user count on the tile beside it.
        AddWorkspace("Personal", _otherUserId, isPersonal: true);

        var project = AddProject("Held", _mine);
        AddTask("Open", project);
        AddTask("Also open", project);
        AddTask("Trashed", project, deletedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.SharedWorkspaceCount);
        Assert.Equal(1, result.DeletedWorkspaceCount);
        Assert.Equal(2, result.TaskCount);
    }

    // Erasing an account soft-deletes its personal workspace and leaves the projects inside
    // it live, so the guard has to reach a level higher than the project's own flag.
    [Fact]
    public async Task GetAdminDashboardAsync_DoesNotCountWorkStrandedInATrashedWorkspace()
    {
        var live = AddProject("Reachable", _mine);
        AddTask("Reachable", live);

        var gone = AddWorkspace("Erased", _otherUserId, deletedAt: DateTime.UtcNow.AddDays(-1));
        var stranded = AddProject("Stranded", gone);
        AddTask("Stranded", stranded);

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ActiveProjectCount);
        Assert.Equal(1, result.TaskCount);
    }

    // Trashing a project does not touch its tasks, so those rows stay !IsDeleted and a bare
    // count keeps counting work the app hides everywhere else.
    [Fact]
    public async Task GetAdminDashboardAsync_DoesNotCountTasksHeldByATrashedProject()
    {
        var live = AddProject("Live", _mine);
        AddTask("Reachable", live);

        var trashed = AddProject("Trashed", _mine, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddTask("Stranded", trashed);
        AddTask("Also stranded", trashed);

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TaskCount);
    }

    // The number the Purge button acts on, so it is asserted either side of the cutoff
    // rather than only past it.
    [Fact]
    public async Task GetAdminDashboardAsync_CountsOnlyProjectsPastTheTrashWindow()
    {
        AddProject("Just deleted", _mine, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddProject(
            "Inside the window",
            _mine,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays - 1))
        );
        AddProject(
            "Past the window",
            _mine,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );
        AddProject("Live", _mine);

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PurgeableProjectCount);
        Assert.Equal(3, result.DeletedProjectCount);
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CountsOnlyAccountsStillLockedOut()
    {
        AddUser("locked@example.com", lockoutEnd: DateTimeOffset.UtcNow.AddMinutes(5));
        AddUser("expired@example.com", lockoutEnd: DateTimeOffset.UtcNow.AddMinutes(-5));
        AddUser("never@example.com");
        // Lockout switched off signs in fine however future the timestamp is, which is what
        // UserManager.IsLockedOutAsync says and therefore what this has to agree with.
        AddUser(
            "exempt@example.com",
            lockoutEnabled: false,
            lockoutEnd: DateTimeOffset.UtcNow.AddMinutes(5)
        );
        // Identity's boundary is >=, so a lockout ending far enough ahead to survive the
        // service sampling its own clock still counts.
        AddUser("edge@example.com", lockoutEnd: DateTimeOffset.UtcNow.AddSeconds(30));
        // A deleted account's stale lockout is not a live one, so the filtered set is read.
        AddUser(
            "deleted@example.com",
            isDeleted: true,
            lockoutEnd: DateTimeOffset.UtcNow.AddMinutes(5)
        );

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.LockedOutUserCount);
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CountsSignupsInsideTheNewUserWindow()
    {
        AddUser("today@example.com", createdAt: DateTime.UtcNow);
        AddUser("recent@example.com", createdAt: DateTime.UtcNow.AddDays(-3));
        AddUser("older@example.com", createdAt: DateTime.UtcNow.AddDays(-30));

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(7, result.NewUserWindowDays);
        Assert.Equal(2, result.NewUserCount);
    }

    [Fact]
    public async Task GetAdminDashboardAsync_NamesTheEnvironment()
    {
        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EnvironmentName, result.Environment);
    }

    public void Dispose() => _context.Dispose();
}
