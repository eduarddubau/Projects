using Backend.Config;
using Backend.Data;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Admin;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services.Admin;

public sealed class AdminWorkspaceServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly AdminWorkspaceService _service;

    private readonly User _owner = null!;

    public AdminWorkspaceServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        _owner = AddUser("owner@example.com", "Ada");
        _currentUser.SetupGet(c => c.UserGuid).Returns(() => _owner.Id);

        _service = new AdminWorkspaceService(_context);
    }

    private User AddUser(string email, string firstName, string? nickname = null)
    {
        var user = new User
        {
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = "Tester",
            Nickname = nickname,
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private Workspace AddWorkspace(
        string name,
        bool isPersonal,
        params (User user, WorkspaceRole role)[] members
    )
    {
        var workspace = new Workspace { Name = name, IsPersonal = isPersonal };

        foreach (var (user, role) in members)
            workspace.Members.Add(
                new WorkspaceMember
                {
                    UserId = user.Id,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                }
            );

        _context.Workspaces.Add(workspace);
        _context.SaveChanges();
        return workspace;
    }

    [Fact]
    public async Task GetAllWorkspacesAsync_ReturnsEveryLiveWorkspaceRegardlessOfMembership()
    {
        var stranger = AddUser("stranger@example.com", "Bob");
        AddWorkspace("Mine", isPersonal: false, (_owner, WorkspaceRole.Owner));
        AddWorkspace("Theirs", isPersonal: false, (stranger, WorkspaceRole.Owner));
        var deleted = AddWorkspace("Gone", isPersonal: false, (stranger, WorkspaceRole.Owner));
        deleted.IsDeleted = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetAllWorkspacesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Mine", "Theirs"], result.Select(w => w.Name));
    }

    [Fact]
    public async Task GetAllDeletedWorkspacesAsync_ReturnsEveryDeletedWorkspace()
    {
        var stranger = AddUser("stranger@example.com", "Bob");
        AddWorkspace("Live", isPersonal: false, (_owner, WorkspaceRole.Owner));
        var deleted = AddWorkspace("Gone", isPersonal: false, (stranger, WorkspaceRole.Owner));
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _service.GetAllDeletedWorkspacesAsync(
            TestContext.Current.CancellationToken
        );

        Assert.Equal(["Gone"], result.Select(w => w.Name));
    }

    [Fact]
    public async Task PurgeWorkspacesAsync_WhenDeleted_HardDeletesItAndItsMembers()
    {
        var workspace = AddWorkspace("Gone", isPersonal: false, (_owner, WorkspaceRole.Owner));
        workspace.IsDeleted = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var count = await _service.PurgeWorkspacesAsync(
            [workspace.Id],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, count);
        Assert.Null(
            await _context
                .Workspaces.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    w => w.Id == workspace.Id,
                    TestContext.Current.CancellationToken
                )
        );
        Assert.Empty(
            await _context
                .WorkspaceMembers.Where(m => m.WorkspaceId == workspace.Id)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task PurgeWorkspacesAsync_WhenNotDeleted_SkipsIt()
    {
        var workspace = AddWorkspace("Live", isPersonal: false, (_owner, WorkspaceRole.Owner));

        var count = await _service.PurgeWorkspacesAsync(
            [workspace.Id],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, count);
        Assert.NotNull(
            await _context.Workspaces.FirstOrDefaultAsync(
                w => w.Id == workspace.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task PurgeWorkspacesAsync_WhenItSomehowStillHoldsAProject_IsRefused()
    {
        var workspace = AddWorkspace("Gone", isPersonal: false, (_owner, WorkspaceRole.Owner));
        workspace.IsDeleted = true;
        _context.Projects.Add(
            new Project
            {
                Name = "Orphan",
                WorkspaceId = workspace.Id,
                IsDeleted = true,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.PurgeWorkspacesAsync([workspace.Id], TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.WorkspaceHasProjects, ex.Code);
    }

    [Fact]
    public async Task RestoreAnyWorkspacesAsync_RestoresRegardlessOfMembership()
    {
        var stranger = AddUser("stranger@example.com", "Bob");
        var workspace = AddWorkspace("Gone", isPersonal: false, (stranger, WorkspaceRole.Owner));
        workspace.IsDeleted = true;
        workspace.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var count = await _service.RestoreWorkspacesAsync(
            [workspace.Id],
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1, count);
        var stored = await _context.Workspaces.FirstAsync(
            w => w.Id == workspace.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    public void Dispose() => _context.Dispose();
}
