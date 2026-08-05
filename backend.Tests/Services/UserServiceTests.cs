using Backend.Config;
using Backend.Data;
using Backend.DTOs.User;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<UserManager<User>> _userManager;
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    // The real normalizer, not a mock: fixtures then produce exactly what Identity
    // writes in production, so they can't drift from the code under test.
    private readonly ILookupNormalizer _normalizer = new UpperInvariantLookupNormalizer();
    private readonly AppDbContext _context;
    private readonly UserService _service;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        var store = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new UserService(
            _context, _currentUser.Object, _userManager.Object, _workspaceService.Object, _normalizer);
    }

    private Workspace AddWorkspace(string name, bool isPersonal, params (User user, WorkspaceRole role)[] members)
    {
        var workspace = new Workspace { Name = name, IsPersonal = isPersonal };

        foreach (var (user, role) in members)
            workspace.Members.Add(new WorkspaceMember
            {
                UserId = user.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });

        _context.Workspaces.Add(workspace);
        _context.SaveChanges();
        return workspace;
    }

    private User AddUser(string email, bool isDeleted = false, string? nickname = null)
    {
        // Normalized fields matter: the reclaim guard in RestoreAnyUserAsync compares them,
        // and under InMemory (LINQ to Objects) two nulls compare equal, so leaving them
        // unset would make every user look like every other user to that guard.
        var user = new User
        {
            Email = email,
            UserName = email,
            NormalizedEmail = _normalizer.NormalizeEmail(email),
            NormalizedUserName = _normalizer.NormalizeName(email),
            FirstName = "Alan",
            LastName = "Turing",
            Nickname = nickname,
            IsDeleted = isDeleted
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenAuthenticated_ReturnsOwnProfile()
    {
        var user = AddUser("me@example.com");
        AddUser("other@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.GetMyProfileAsync();

        Assert.NotNull(result);
        Assert.Equal("me@example.com", result!.Email);
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenNotAuthenticated_ReturnsNull()
    {
        AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns((Guid?)null);

        var result = await _service.GetMyProfileAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenUserIsDeleted_ReturnsNull()
    {
        var user = AddUser("me@example.com", isDeleted: true);
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.GetMyProfileAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_UpdatesOwnNames()
    {
        var user = AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest { FirstName = "Grace", LastName = "Hopper" });

        Assert.NotNull(result);
        Assert.Equal("Grace", result!.FirstName);
        Assert.Equal("Hopper", result.LastName);

        var stored = await _context.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal("Grace", stored.FirstName);
        Assert.Equal("Hopper", stored.LastName);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_UpdatesNickname()
    {
        var user = AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest { FirstName = "Grace", LastName = "Hopper", Nickname = "Amazing Grace" });

        Assert.Equal("Amazing Grace", result!.Nickname);

        var stored = await _context.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal("Amazing Grace", stored.Nickname);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithoutNickname_ClearsExistingOne()
    {
        var user = AddUser("me@example.com", nickname: "Amazing Grace");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest { FirstName = "Grace", LastName = "Hopper" });

        Assert.Null(result!.Nickname);

        var stored = await _context.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Null(stored.Nickname);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WhenNotAuthenticated_ReturnsNull()
    {
        AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns((Guid?)null);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest { FirstName = "Grace", LastName = "Hopper" });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WhenUserIsDeleted_ReturnsNull()
    {
        var user = AddUser("me@example.com", isDeleted: true);
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest { FirstName = "Grace", LastName = "Hopper" });

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllUsersIncludingDeleted()
    {
        AddUser("active@example.com");
        AddUser("deleted@example.com", isDeleted: true);

        var result = await _service.GetAllUsersAsync();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetAnyUserByIdAsync_WhenFound_ReturnsDto()
    {
        var user = AddUser("ada@example.com");

        var result = await _service.GetAnyUserByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Email, result!.Email);
    }

    [Fact]
    public async Task GetAnyUserByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.GetAnyUserByIdAsync(Guid.NewGuid());

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

        var request = new CreateUserRequest { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };

        var result = await _service.CreateUserAsync(request);

        Assert.Equal("ada@example.com", result.Email);
        _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), AppRoles.User), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailAlreadyExists_ThrowsBusinessRuleException()
    {
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync(new User { Email = "ada@example.com", FirstName = "Ada", LastName = "Lovelace" });

        var request = new CreateUserRequest { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateUserAsync(request));
    }

    [Fact]
    public async Task CreateUserAsync_WhenIdentityCreationFails_ThrowsBusinessRuleException()
    {
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync((User?)null);
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

        var request = new CreateUserRequest { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateUserAsync(request));
    }

    [Fact]
    public async Task DeleteAnyUserAsync_WhenFound_SoftDeletesAndReturnsTrue()
    {
        var user = AddUser("ada@example.com");

        var result = await _service.DeleteAnyUserAsync(user.Id);

        Assert.True(result);
        var stored = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteAnyUserAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteAnyUserAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenDeleted_RestoresAndReturnsDto()
    {
        var user = AddUser("ada@example.com", isDeleted: true);

        var result = await _service.RestoreAnyUserAsync(user.Id);

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
        var stored = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.RestoreAnyUserAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenAddressWasReclaimed_Throws()
    {
        // Uniqueness is scoped to live rows, so restoring would put this row back into the
        // partial index and Postgres would reject it. The guard turns that 500 into a 409.
        var deleted = AddUser("ada@example.com", isDeleted: true);
        AddUser("ada@example.com");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.RestoreAnyUserAsync(deleted.Id));

        Assert.Equal(BusinessRuleCodes.EmailReclaimed, ex.Code);
        Assert.Equal("ada@example.com", ex.Params?["email"]);

        var stored = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == deleted.Id);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task RestoreAnyUserAsync_WhenAnotherAddressIsLive_Restores()
    {
        var deleted = AddUser("ada@example.com", isDeleted: true);
        AddUser("grace@example.com");

        var result = await _service.RestoreAnyUserAsync(deleted.Id);

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

        var result = await _service.RestoreAnyUserAsync(deleted.Id);

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
    }

    [Fact]
    public async Task GetDeletedUsersAsync_ReturnsOnlyDeletedUsers()
    {
        AddUser("active@example.com");
        AddUser("deleted@example.com", isDeleted: true);

        var result = await _service.GetDeletedUsersAsync();

        Assert.Equal(["deleted@example.com"], result.Select(u => u.Email));
    }

    [Fact]
    public async Task GetDeletedUsersAsync_ExcludesAnonymizedUsers()
    {
        AddUser("deleted@example.com", isDeleted: true);
        var anonymized = AddUser("erased@example.com", isDeleted: true);
        anonymized.IsAnonymized = true;
        _context.SaveChanges();

        var result = await _service.GetDeletedUsersAsync();

        Assert.Equal(["deleted@example.com"], result.Select(u => u.Email));
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenDeleted_ScrubsPiiAndKeepsRow()
    {
        var user = AddUser("jane@example.com", isDeleted: true, nickname: "Janey");

        var result = await _service.AnonymizeUserAsync(user.Id);

        Assert.True(result);

        var stored = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
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

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AnonymizeUserAsync(user.Id));

        Assert.Equal(BusinessRuleCodes.SoleOwnerOfWorkspaces, ex.Code);
        Assert.Contains("Acme Team", ex.Message);
        // The client renders its own text, so the names have to travel as data too.
        Assert.Equal("Acme Team", ex.Params!["workspaces"]);

        // The erasure must not be half-applied when it is refused.
        var stored = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        Assert.False(stored.IsAnonymized);
        Assert.Equal("owner@example.com", stored.Email);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenSharedWorkspaceHasAnotherOwner_SucceedsAndDropsMembership()
    {
        var user = AddUser("leaving@example.com", isDeleted: true);
        var coOwner = AddUser("staying@example.com");
        AddWorkspace("Acme Team", isPersonal: false,
            (user, WorkspaceRole.Owner), (coOwner, WorkspaceRole.Owner));

        var result = await _service.AnonymizeUserAsync(user.Id);

        Assert.True(result);
        Assert.Empty(await _context.WorkspaceMembers.Where(m => m.UserId == user.Id).ToListAsync());
        Assert.Single(await _context.WorkspaceMembers.Where(m => m.UserId == coOwner.Id).ToListAsync());
    }

    [Fact]
    public async Task AnonymizeUserAsync_MemberButNotOwner_IsNotBlocked()
    {
        var user = AddUser("member@example.com", isDeleted: true);
        var owner = AddUser("boss@example.com");
        AddWorkspace("Acme Team", isPersonal: false,
            (owner, WorkspaceRole.Owner), (user, WorkspaceRole.Member));

        Assert.True(await _service.AnonymizeUserAsync(user.Id));
    }

    [Fact]
    public async Task AnonymizeUserAsync_SoftDeletesPersonalWorkspaceRatherThanPurgingIt()
    {
        var user = AddUser("solo@example.com", isDeleted: true);
        var personal = AddWorkspace("Solo's Workspace", isPersonal: true, (user, WorkspaceRole.Owner));

        Assert.True(await _service.AnonymizeUserAsync(user.Id));

        // Row survives so audit foreign keys stay valid, but nothing lists it.
        var stored = await _context.Workspaces.IgnoreQueryFilters().FirstAsync(w => w.Id == personal.Id);
        Assert.True(stored.IsDeleted);
        Assert.Empty(await _context.Workspaces.Where(w => w.Id == personal.Id).ToListAsync());
    }

    [Fact]
    public async Task CreateUserAsync_EnsuresPersonalWorkspace()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        await _service.CreateUserAsync(new CreateUserRequest
        {
            Email = "new@example.com",
            FirstName = "Grace",
            LastName = "Hopper"
        });

        _workspaceService.Verify(
            w => w.EnsurePersonalWorkspaceAsync(
                It.Is<User>(u => u.Email == "new@example.com"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenNotDeleted_ReturnsFalse()
    {
        var user = AddUser("active@example.com");

        var result = await _service.AnonymizeUserAsync(user.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenAlreadyAnonymized_ReturnsFalse()
    {
        var user = AddUser("erased@example.com", isDeleted: true);
        user.IsAnonymized = true;
        _context.SaveChanges();

        var result = await _service.AnonymizeUserAsync(user.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task AnonymizeUserAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.AnonymizeUserAsync(Guid.NewGuid());

        Assert.False(result);
    }
}
