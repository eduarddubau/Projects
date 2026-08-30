using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Services.Admin;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services.Admin;

public sealed class AdminDashboardServiceTests : IDisposable
{
    // The window the *user* trash applies. Admin trash deliberately ignores it.
    private const int UserTrashWindowDays = 30;

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
        _service = new AdminDashboardService(_context);

        _currentUser.Setup(c => c.UserGuid).Returns(_userId);

        _mine = AddWorkspace("Mine", _userId);
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

    private User AddUser(
        string email,
        bool isDeleted = false,
        bool isAnonymized = false,
        DateTime? createdAt = null
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
        // The admin trash has no window, so even old deletions count.
        AddProject(
            "Old deleted",
            _theirs,
            createdBy: owner.Id,
            deletedAt: DateTime.UtcNow.AddDays(-(UserTrashWindowDays + 10))
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

    public void Dispose() => _context.Dispose();
}
