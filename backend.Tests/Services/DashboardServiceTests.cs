using Backend.Config;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.Tests.Services;

public sealed class DashboardServiceTests : IDisposable
{
    private const int TrashWindowDays = 30;

    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly DashboardService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);
        _service = new DashboardService(
            _context,
            _currentUser.Object,
            Options.Create(new ProjectRetentionOptions { TrashWindowDays = TrashWindowDays }));

        _currentUser.Setup(c => c.UserGuid).Returns(_userId);
    }

    private User AddUser(string email, bool isDeleted = false, bool isAnonymized = false,
        DateTime? createdAt = null)
    {
        var user = new User
        {
            Email = email,
            UserName = email,
            FirstName = "Alan",
            LastName = "Turing",
            IsDeleted = isDeleted,
            IsAnonymized = isAnonymized,
            CreatedAt = createdAt ?? default
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    // Audit stamping lives in SaveChangesAsync only, so the sync save here
    // persists these timestamps untouched.
    private Project AddProject(string name, Guid createdBy, DateTime? deletedAt = null,
        DateTime? createdAt = null, DateTime? updatedAt = null)
    {
        var project = new Project
        {
            Name = name,
            CreatedBy = createdBy,
            IsDeleted = deletedAt is not null,
            DeletedAt = deletedAt,
            CreatedAt = createdAt ?? default,
            UpdatedAt = updatedAt
        };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    [Fact]
    public async Task GetMyDashboardAsync_ReturnsCountsRecentAndLastActivity()
    {
        AddProject("Older", _userId, createdAt: DateTime.UtcNow.AddDays(-10));
        var newest = AddProject("Newest", _userId,
            createdAt: DateTime.UtcNow.AddDays(-8), updatedAt: DateTime.UtcNow.AddDays(-1));
        AddProject("Trashed", _userId, deletedAt: DateTime.UtcNow.AddDays(-5));
        AddProject("Expired trash", _userId, deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1)));
        AddProject("Someone else's", Guid.NewGuid(), createdAt: DateTime.UtcNow);

        var result = await _service.GetMyDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ActiveProjectCount);
        Assert.Equal(1, result.DeletedProjectCount);
        Assert.Equal(["Newest", "Older"], result.RecentProjects.Select(p => p.Name));
        Assert.Equal(newest.UpdatedAt, result.LastActivityAt);
    }

    [Fact]
    public async Task GetMyDashboardAsync_CapsRecentProjectsAtFive()
    {
        for (var i = 0; i < 7; i++)
            AddProject($"Project {i}", _userId, createdAt: DateTime.UtcNow.AddDays(-i));

        var result = await _service.GetMyDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(7, result.ActiveProjectCount);
        Assert.Equal(5, result.RecentProjects.Count);
    }

    [Fact]
    public async Task GetMyDashboardAsync_WhenNoProjects_ReturnsZerosAndNoActivity()
    {
        AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.GetMyDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ActiveProjectCount);
        Assert.Equal(0, result.DeletedProjectCount);
        Assert.Null(result.LastActivityAt);
        Assert.Empty(result.RecentProjects);
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CountsAcrossAllUsers()
    {
        var owner = AddUser("owner@example.com", createdAt: DateTime.UtcNow.AddDays(-3));
        AddUser("deleted@example.com", isDeleted: true, createdAt: DateTime.UtcNow.AddDays(-2));
        AddUser("erased@example.com", isDeleted: true, isAnonymized: true, createdAt: DateTime.UtcNow.AddDays(-1));

        AddProject("Mine", _userId, createdAt: DateTime.UtcNow.AddDays(-2));
        AddProject("Theirs", owner.Id, createdAt: DateTime.UtcNow.AddDays(-1));
        // Admin trash has no retention window, so even old deletions count.
        AddProject("Old deleted", owner.Id, deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 10)));

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ActiveProjectCount);
        Assert.Equal(1, result.DeletedProjectCount);
        Assert.Equal(1, result.ActiveUserCount);
        Assert.Equal(1, result.DeletedUserCount);
        Assert.Equal(["Theirs", "Mine"], result.RecentProjects.Select(p => p.Name));
        Assert.Equal(["owner@example.com"], result.RecentUsers.Select(u => u.Email));
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CapsRecentListsAtFive()
    {
        for (var i = 0; i < 7; i++)
        {
            AddUser($"user{i}@example.com", createdAt: DateTime.UtcNow.AddDays(-i));
            AddProject($"Project {i}", _userId, createdAt: DateTime.UtcNow.AddDays(-i));
        }

        var result = await _service.GetAdminDashboardAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, result.RecentProjects.Count);
        Assert.Equal(5, result.RecentUsers.Count);
    }

    public void Dispose() => _context.Dispose();
}
