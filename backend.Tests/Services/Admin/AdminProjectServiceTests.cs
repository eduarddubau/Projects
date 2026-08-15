using Backend.Config;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Services.Admin;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.Tests.Services.Admin;

public sealed class AdminProjectServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly AdminProjectService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private const int TrashWindowDays = 30;

    // Two workspaces with no member in common, so every read here has to cross one.
    private readonly Workspace _personal;
    private readonly Workspace _othersWorkspace;

    public AdminProjectServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);
        _currentUser.Setup(c => c.UserGuid).Returns(_userId);

        _service = new AdminProjectService(
            _context,
            Options.Create(new ProjectRetentionOptions { TrashWindowDays = TrashWindowDays })
        );

        _personal = AddWorkspace("Personal", isPersonal: true, (_userId, WorkspaceRole.Owner));
        _othersWorkspace = AddWorkspace(
            "Others",
            isPersonal: false,
            (_otherUserId, WorkspaceRole.Owner)
        );
    }

    private Workspace AddWorkspace(
        string name,
        bool isPersonal,
        params (Guid UserId, WorkspaceRole Role)[] members
    )
    {
        var workspace = new Workspace { Name = name, IsPersonal = isPersonal };

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

    private Project AddProject(
        string name,
        Workspace workspace,
        Guid? createdBy = null,
        bool isDeleted = false,
        DateTime? deletedAt = null
    )
    {
        var project = new Project
        {
            Name = name,
            CreatedBy = createdBy ?? _userId,
            WorkspaceId = workspace.Id,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
        };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAllProjectsAsync_ReturnsProjectsFromEveryWorkspace()
    {
        AddProject("Mine", _personal);
        AddProject("Foreign", _othersWorkspace, createdBy: _otherUserId);

        var result = await _service.GetAllProjectsAsync(Ct);

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetAnyProjectByIdAsync_ReturnsProjectRegardlessOfMembership()
    {
        var project = AddProject("Foreign", _othersWorkspace, createdBy: _otherUserId);

        var result = await _service.GetProjectByIdAsync(project.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(project.Id, result!.Id);
    }

    [Fact]
    public async Task DeleteAnyProjectByIdAsync_SoftDeletesRegardlessOfMembership()
    {
        var project = AddProject("Foreign", _othersWorkspace, createdBy: _otherUserId);

        var result = await _service.DeleteProjectByIdAsync(project.Id, Ct);

        Assert.True(result);
        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == project.Id, Ct);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteAnyProjectByIdAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteProjectByIdAsync(Guid.NewGuid(), Ct);

        Assert.False(result);
    }

    [Fact]
    public async Task RestoreAnyProjectsAsync_WhenDeleted_RestoresAndReturnsCount()
    {
        var project = AddProject("Deleted", _personal, isDeleted: true);

        var result = await _service.RestoreProjectsAsync([project.Id], Ct);

        Assert.Equal(1, result);
        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == project.Id, Ct);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    [Fact]
    public async Task RestoreAnyProjectsAsync_WhenNotFound_ReturnsZero()
    {
        var result = await _service.RestoreProjectsAsync([Guid.NewGuid()], Ct);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RestoreAnyProjectsAsync_WithMultipleIds_RestoresAllAndReturnsCount()
    {
        var deleted1 = AddProject("Deleted1", _personal, isDeleted: true);
        var deleted2 = AddProject("Deleted2", _personal, isDeleted: true);

        var result = await _service.RestoreProjectsAsync([deleted1.Id, deleted2.Id], Ct);

        Assert.Equal(2, result);
        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .Where(p => p.Id == deleted1.Id || p.Id == deleted2.Id)
            .ToListAsync(Ct);
        Assert.All(stored, p => Assert.False(p.IsDeleted));
    }

    [Fact]
    public async Task GetAllDeletedProjectsAsync_ReturnsOnlyDeletedProjects()
    {
        AddProject("Active", _personal);
        AddProject("Deleted", _personal, isDeleted: true);

        var result = await _service.GetAllDeletedProjectsAsync(Ct);

        Assert.Equal(["Deleted"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetAllDeletedProjectsAsync_ReturnsAllDeletedRegardlessOfAge()
    {
        AddProject(
            "RecentlyDeleted",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-1)
        );
        AddProject(
            "OldDeleted",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );

        var result = await _service.GetAllDeletedProjectsAsync(Ct);

        Assert.Equal(["OldDeleted", "RecentlyDeleted"], result.Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task GetAllDeletedProjectsAsync_FlagsOnlyProjectsOlderThanRetentionWindowAsPurgeable()
    {
        AddProject(
            "RecentlyDeleted",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-1)
        );
        AddProject(
            "OldDeleted",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );

        var result = await _service.GetAllDeletedProjectsAsync(Ct);

        Assert.False(result.Single(p => p.Name == "RecentlyDeleted").IsPurgeable);
        Assert.True(result.Single(p => p.Name == "OldDeleted").IsPurgeable);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WhenDeleted_HardDeletesAndReturnsCount()
    {
        var project = AddProject("Deleted", _personal, isDeleted: true);

        var result = await _service.PurgeProjectsAsync([project.Id], Ct);

        Assert.Equal(1, result);
        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == project.Id, Ct);
        Assert.Null(stored);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WhenNotDeleted_SkipsItAndDoesNotDelete()
    {
        var project = AddProject("Active", _personal);

        var result = await _service.PurgeProjectsAsync([project.Id], Ct);

        Assert.Equal(0, result);
        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == project.Id, Ct);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WhenNotFound_ReturnsZero()
    {
        var result = await _service.PurgeProjectsAsync([Guid.NewGuid()], Ct);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WithMultipleIds_PurgesOnlyTheDeletedOnesAndReturnsCount()
    {
        var deleted1 = AddProject("Deleted1", _personal, isDeleted: true);
        var deleted2 = AddProject("Deleted2", _personal, isDeleted: true);
        var active = AddProject("Active", _personal);

        var result = await _service.PurgeProjectsAsync([deleted1.Id, deleted2.Id, active.Id], Ct);

        Assert.Equal(2, result);
        var remaining = await _context
            .Projects.IgnoreQueryFilters()
            .Select(p => p.Id)
            .ToListAsync(Ct);
        Assert.Equal([active.Id], remaining);
    }

    public void Dispose() => _context.Dispose();
}
