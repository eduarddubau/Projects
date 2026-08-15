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

public sealed class ProfileServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<UserManager<User>> _userManager;

    // The real normalizer, not a mock: fixtures then produce exactly what Identity
    // writes in production, so they can't drift from the code under test.
    private readonly UpperInvariantLookupNormalizer _normalizer = new();
    private readonly AppDbContext _context;
    private readonly ProfileService _service;

    public ProfileServiceTests()
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

        _service = new ProfileService(
            _context,
            _currentUser.Object,
            _userManager.Object,
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
    public async Task GetMyProfileAsync_WhenAuthenticated_ReturnsOwnProfile()
    {
        var user = AddUser("me@example.com");
        AddUser("other@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.GetMyProfileAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("me@example.com", result!.Email);
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenNotAuthenticated_ReturnsNull()
    {
        AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns((Guid?)null);

        var result = await _service.GetMyProfileAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMyProfileAsync_WhenUserIsDeleted_ReturnsNull()
    {
        var user = AddUser("me@example.com", isDeleted: true);
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.GetMyProfileAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_UpdatesOwnNames()
    {
        var user = AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Email = "me@example.com",
            },
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal("Grace", result!.FirstName);
        Assert.Equal("Hopper", result.LastName);

        var stored = await _context.Users.FirstAsync(
            u => u.Id == user.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal("Grace", stored.FirstName);
        Assert.Equal("Hopper", stored.LastName);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_UpdatesNickname()
    {
        var user = AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Nickname = "Amazing Grace",
                Email = "me@example.com",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Amazing Grace", result!.Nickname);

        var stored = await _context.Users.FirstAsync(
            u => u.Id == user.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal("Amazing Grace", stored.Nickname);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WithoutNickname_ClearsExistingOne()
    {
        var user = AddUser("me@example.com", nickname: "Amazing Grace");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Email = "me@example.com",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Null(result!.Nickname);

        var stored = await _context.Users.FirstAsync(
            u => u.Id == user.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Null(stored.Nickname);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WhenEmailDiffers_CallsSetEmail()
    {
        var user = AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);
        _userManager
            .Setup(m => m.SetEmailAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Email = "new@example.com",
            },
            TestContext.Current.CancellationToken
        );

        // Asserting the decision, not the mutation: a mocked UserManager never actually
        // changes the entity, so "the email changed" could only ever pass vacuously.
        _userManager.Verify(
            m => m.SetEmailAsync(It.Is<User>(u => u.Id == user.Id), "new@example.com"),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WhenEmailOnlyDiffersByCase_DoesNotCallSetEmail()
    {
        // The comparison is on the normalized value. Without that, re-saving the profile with
        // a differently-cased address would reset EmailConfirmed every time.
        var user = AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Email = "ME@Example.COM",
            },
            TestContext.Current.CancellationToken
        );

        _userManager.Verify(
            m => m.SetEmailAsync(It.IsAny<User>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WhenSetEmailFails_ThrowsDuplicateEmail()
    {
        var user = AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);
        _userManager
            .Setup(m => m.SetEmailAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(
                IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "Email 'taken@example.com' is already taken.",
                    }
                )
            );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateMyProfileAsync(
                new UpdateProfileRequest
                {
                    FirstName = "Grace",
                    LastName = "Hopper",
                    Email = "taken@example.com",
                },
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(BusinessRuleCodes.DuplicateEmail, ex.Code);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WhenNotAuthenticated_ReturnsNull()
    {
        AddUser("me@example.com");
        _currentUser.Setup(c => c.UserGuid).Returns((Guid?)null);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Email = "me@example.com",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMyProfileAsync_WhenUserIsDeleted_ReturnsNull()
    {
        var user = AddUser("me@example.com", isDeleted: true);
        _currentUser.Setup(c => c.UserGuid).Returns(user.Id);

        var result = await _service.UpdateMyProfileAsync(
            new UpdateProfileRequest
            {
                FirstName = "Grace",
                LastName = "Hopper",
                Email = "me@example.com",
            },
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    public void Dispose() => _context.Dispose();
}
