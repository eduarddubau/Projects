using Backend.Config;
using Backend.Data;
using Backend.DTOs.User;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Admin;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services.Admin;

public sealed class AdminUserServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<UserManager<User>> _userManager;
    private readonly Mock<IWorkspaceService> _workspaceService = new();

    // The real normalizer, not a mock: fixtures then produce exactly what Identity
    // writes in production, so they can't drift from the code under test.
    private readonly UpperInvariantLookupNormalizer _normalizer = new();
    private readonly AppDbContext _context;
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        var store = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );

        _service = new AdminUserService(
            _context,
            _userManager.Object,
            _workspaceService.Object,
            _normalizer
        );
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

    private User AddUser(string email, bool isDeleted = false, string? nickname = null)
    {
        // Normalized fields matter: the reclaim guard in RestoreUserAsync compares them,
        // and under InMemory (LINQ to Objects) two nulls compare equal, so leaving them
        // unset would make every user look like every other user to that guard.
        // Mirrors ToEntity: UserName is derived from the id, not the email, so fixtures
        // can't set up a username collision that production can no longer produce.
        var id = Guid.CreateVersion7();
        var user = new User
        {
            Id = id,
            Email = email,
            UserName = id.ToString("N"),
            NormalizedEmail = _normalizer.NormalizeEmail(email),
            NormalizedUserName = _normalizer.NormalizeName(id.ToString("N")),
            FirstName = "Alan",
            LastName = "Turing",
            Nickname = nickname,
            IsDeleted = isDeleted,
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllUsersIncludingDeleted()
    {
        AddUser("active@example.com");
        AddUser("deleted@example.com", isDeleted: true);

        var result = await _service.GetAllUsersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetAnyUserByIdAsync_WhenFound_ReturnsDto()
    {
        var user = AddUser("ada@example.com");

        var result = await _service.GetUserByIdAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(user.Email, result!.Email);
    }

    [Fact]
    public async Task GetAnyUserByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.GetUserByIdAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailIsNew_CreatesUserWithUserRole()
    {
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync((User?)null);
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager
            .Setup(m => m.AddToRoleAsync(It.IsAny<User>(), AppRoles.User))
            .ReturnsAsync(IdentityResult.Success);

        var request = new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
        };

        var result = await _service.CreateUserAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("ada@example.com", result.Email);
        _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), AppRoles.User), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailAlreadyExists_ThrowsBusinessRuleException()
    {
        _userManager
            .Setup(m => m.FindByEmailAsync("ada@example.com"))
            .ReturnsAsync(
                new User
                {
                    Email = "ada@example.com",
                    FirstName = "Ada",
                    LastName = "Lovelace",
                }
            );

        var request = new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateUserAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateUserAsync_WhenIdentityCreationFails_ThrowsBusinessRuleException()
    {
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync((User?)null);
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(
                IdentityResult.Failed(new IdentityError { Description = "Password too weak." })
            );

        var request = new CreateUserRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateUserAsync(request, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task DeleteAnyUserAsync_WhenFound_SoftDeletesAndReturnsTrue()
    {
        var user = AddUser("ada@example.com");

        var result = await _service.DeleteUserAsync(user.Id, TestContext.Current.CancellationToken);

        Assert.True(result);
        var stored = await _context
            .Users.IgnoreQueryFilters()
            .FirstAsync(
                u => u.Id == user.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteAnyUserAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteUserAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenDeleted_RestoresAndReturnsDto()
    {
        var user = AddUser("ada@example.com", isDeleted: true);

        var result = await _service.RestoreUserAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
        var stored = await _context
            .Users.IgnoreQueryFilters()
            .FirstAsync(
                u => u.Id == user.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.RestoreUserAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenAddressWasReclaimed_Throws()
    {
        // Uniqueness is scoped to live rows, so restoring would put this row back into the
        // partial index and Postgres would reject it. The guard turns that 500 into a 409.
        var deleted = AddUser("ada@example.com", isDeleted: true);
        AddUser("ada@example.com");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RestoreUserAsync(deleted.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.EmailReclaimed, ex.Code);
        Assert.Equal("ada@example.com", ex.Params?["email"]);

        var stored = await _context
            .Users.IgnoreQueryFilters()
            .FirstAsync(
                u => u.Id == deleted.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenAnotherAddressIsLive_Restores()
    {
        var deleted = AddUser("ada@example.com", isDeleted: true);
        AddUser("grace@example.com");

        var result = await _service.RestoreUserAsync(
            deleted.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenOnlyADeletedUserHoldsTheAddress_Restores()
    {
        // The guard queries _context.Users *with* the !IsDeleted filter on purpose: another
        // deleted row isn't in the partial index either, so it can't block a restore.
        var deleted = AddUser("ada@example.com", isDeleted: true);
        AddUser("ada@example.com", isDeleted: true);

        var result = await _service.RestoreUserAsync(
            deleted.Id,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
    }

    [Fact]
    public async Task GetDeletedUsersAsync_ReturnsOnlyDeletedUsers()
    {
        AddUser("active@example.com");
        AddUser("deleted@example.com", isDeleted: true);

        var result = await _service.GetDeletedUsersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["deleted@example.com"], result.Select(u => u.Email));
    }

    [Fact]
    public async Task GetDeletedUsersAsync_ExcludesAnonymizedUsers()
    {
        AddUser("deleted@example.com", isDeleted: true);
        var anonymized = AddUser("erased@example.com", isDeleted: true);
        anonymized.IsAnonymized = true;
        _context.SaveChanges();

        var result = await _service.GetDeletedUsersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["deleted@example.com"], result.Select(u => u.Email));
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenDeleted_ScrubsPiiAndKeepsRow()
    {
        var user = AddUser("jane@example.com", isDeleted: true, nickname: "Janey");

        var result = await _service.AnonymizeUserAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        Assert.True(result);

        var stored = await _context
            .Users.IgnoreQueryFilters()
            .FirstAsync(
                u => u.Id == user.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
        Assert.True(stored.IsAnonymized);
        Assert.NotNull(stored.AnonymizedAt);
        Assert.True(stored.IsDeleted);
        Assert.Equal("Deleted", stored.FirstName);
        Assert.Equal("User", stored.LastName);
        Assert.Null(stored.Nickname);
        Assert.DoesNotContain("jane@example.com", stored.Email);
        Assert.Null(stored.PasswordHash);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenSoleOwnerOfSharedWorkspace_ThrowsAndLeavesUserIntact()
    {
        var user = AddUser("owner@example.com", isDeleted: true);
        AddWorkspace("Acme Team", isPersonal: false, (user, WorkspaceRole.Owner));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.AnonymizeUserAsync(user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.SoleOwnerOfWorkspaces, ex.Code);
        Assert.Contains("Acme Team", ex.Message);
        // The client renders its own text, so the names have to travel as data too.
        Assert.Equal("Acme Team", ex.Params!["workspaces"]);

        // The erasure must not be half-applied when it is refused.
        var stored = await _context
            .Users.IgnoreQueryFilters()
            .FirstAsync(
                u => u.Id == user.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
        Assert.False(stored.IsAnonymized);
        Assert.Equal("owner@example.com", stored.Email);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenSharedWorkspaceHasAnotherOwner_SucceedsAndDropsMembership()
    {
        var user = AddUser("leaving@example.com", isDeleted: true);
        var coOwner = AddUser("staying@example.com");
        AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (user, WorkspaceRole.Owner),
            (coOwner, WorkspaceRole.Owner)
        );

        var result = await _service.AnonymizeUserAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        Assert.True(result);
        Assert.Empty(
            await _context
                .WorkspaceMembers.Where(m => m.UserId == user.Id)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Single(
            await _context
                .WorkspaceMembers.Where(m => m.UserId == coOwner.Id)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task AnonymizeUserAsync_MemberButNotOwner_IsNotBlocked()
    {
        var user = AddUser("member@example.com", isDeleted: true);
        var owner = AddUser("boss@example.com");
        AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (owner, WorkspaceRole.Owner),
            (user, WorkspaceRole.Member)
        );

        Assert.True(
            await _service.AnonymizeUserAsync(user.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task AnonymizeUserAsync_SoftDeletesPersonalWorkspaceRatherThanPurgingIt()
    {
        var user = AddUser("solo@example.com", isDeleted: true);
        var personal = AddWorkspace(
            "Solo's Workspace",
            isPersonal: true,
            (user, WorkspaceRole.Owner)
        );

        Assert.True(
            await _service.AnonymizeUserAsync(user.Id, TestContext.Current.CancellationToken)
        );

        // Row survives so audit foreign keys stay valid, but nothing lists it.
        var stored = await _context
            .Workspaces.IgnoreQueryFilters()
            .FirstAsync(
                w => w.Id == personal.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
        Assert.True(stored.IsDeleted);
        Assert.Empty(
            await _context
                .Workspaces.Where(w => w.Id == personal.Id)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CreateUserAsync_EnsuresPersonalWorkspace()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager
            .Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        await _service.CreateUserAsync(
            new CreateUserRequest
            {
                Email = "new@example.com",
                FirstName = "Grace",
                LastName = "Hopper",
            },
            TestContext.Current.CancellationToken
        );

        _workspaceService.Verify(
            w =>
                w.EnsurePersonalWorkspaceAsync(
                    It.Is<User>(u => u.Email == "new@example.com"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenNotDeleted_ReturnsFalse()
    {
        var user = AddUser("active@example.com");

        var result = await _service.AnonymizeUserAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        Assert.False(result);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenAlreadyAnonymized_ReturnsFalse()
    {
        var user = AddUser("erased@example.com", isDeleted: true);
        user.IsAnonymized = true;
        _context.SaveChanges();

        var result = await _service.AnonymizeUserAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        Assert.False(result);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.AnonymizeUserAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result);
    }

    public void Dispose() => _context.Dispose();
}
