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

        _service = new UserService(_context, _currentUser.Object, _userManager.Object);
    }

    private User AddUser(string email, bool isDeleted = false)
    {
        var user = new User { Email = email, UserName = email, IsDeleted = isDeleted };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
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
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync(new User { Email = "ada@example.com" });

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
        var user = AddUser("jane@example.com", isDeleted: true);

        var result = await _service.AnonymizeUserAsync(user.Id);

        Assert.True(result);

        var stored = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        Assert.True(stored.IsAnonymized);
        Assert.NotNull(stored.AnonymizedAt);
        Assert.True(stored.IsDeleted);
        Assert.Equal("Deleted", stored.FirstName);
        Assert.Equal("User", stored.LastName);
        Assert.DoesNotContain("jane@example.com", stored.Email);
        Assert.Null(stored.PasswordHash);
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
