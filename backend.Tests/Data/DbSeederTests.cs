using Backend.Config;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Backend.Tests.Data;

public sealed class DbSeederTests : IDisposable
{
    private readonly Mock<UserManager<User>> _userManager;
    private readonly UpperInvariantLookupNormalizer _normalizer = new();
    private readonly Mock<RoleManager<IdentityRole<Guid>>> _roleManager;
    private readonly AppDbContext _context;
    private readonly ILogger _logger = new Mock<ILogger>().Object;
    private readonly ProjectRetentionOptions _retention = new() { TrashWindowDays = 30 };
    private readonly IWorkspaceService _workspaceService;

    public DbSeederTests()
    {
        var userStore = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        _roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStore.Object, null!, null!, null!, null!);
        _roleManager.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _roleManager.Setup(r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>())).ReturnsAsync(IdentityResult.Success);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = new Mock<ICurrentUserService>().Object;
        _context = new AppDbContext(options, currentUser);

        // The real service, not a mock: the seeder now delegates personal-workspace creation
        // to it, so these tests assert on its actual behaviour (naming, owner membership).
        _workspaceService = new WorkspaceService(
            _context, currentUser, new WorkspaceAccessService(_context, currentUser));
    }

    private Task Seed(AdminSeedOptions admin, bool isDevelopment) =>
        DbSeeder.SeedAsync(_userManager.Object, _roleManager.Object, _context, _logger, _retention, admin,
            _normalizer, _workspaceService, isDevelopment);

    /// <summary>Admin options whose account already exists, so admin seeding is a no-op and the
    /// test can focus on what follows it.</summary>
    private AdminSeedOptions ExistingAdmin()
    {
        var admin = new AdminSeedOptions { Email = "admin@acme.com", Password = "Adm1n!Secure9" };
        _userManager.Setup(m => m.FindByEmailAsync(admin.Email))
            .ReturnsAsync(new User { Email = admin.Email, UserName = admin.Email, FirstName = "Site", LastName = "Admin" });
        return admin;
    }

    /// <summary>UserManager is mocked, so users it "creates" never reach the context —
    /// personal-workspace seeding reads the context, so seed users into it directly.</summary>
    private User AddUser(string email, string firstName, string? nickname = null)
    {
        var user = new User
        {
            Email = email,
            UserName = email,
            // UserManager normalizes on create; the seeder looks users up by it.
            NormalizedEmail = _normalizer.NormalizeEmail(email),
            NormalizedUserName = _normalizer.NormalizeName(email),
            FirstName = firstName,
            LastName = "Tester",
            Nickname = nickname
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    [Fact]
    public async Task SeedAsync_GivesEveryUserAPersonalWorkspaceOwnedByThem()
    {
        var admin = ExistingAdmin();
        AddUser("alan@example.com", "Alan");
        AddUser("grace@example.com", "Grace", nickname: "Amazing");

        await Seed(admin, isDevelopment: false);

        // Include: there are no lazy-loading proxies, so Members is empty without it.
        var personal = await _context.Workspaces
            .Include(w => w.Members)
            .Where(w => w.IsPersonal)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, personal.Count);
        Assert.Contains(personal, w => w.Name == "Alan's Workspace");
        Assert.Contains(personal, w => w.Name == "Amazing's Workspace");
        Assert.All(personal, w => Assert.Single(w.Members, m => m.Role == WorkspaceRole.Owner));
    }

    [Fact]
    public async Task SeedAsync_BlankNicknameFallsBackToFirstName()
    {
        var admin = ExistingAdmin();
        AddUser("alan@example.com", "Alan", nickname: "  ");

        await Seed(admin, isDevelopment: false);

        Assert.Equal("Alan's Workspace", (await _context.Workspaces.FirstAsync(w => w.IsPersonal, cancellationToken: TestContext.Current.CancellationToken)).Name);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicatePersonalWorkspaces()
    {
        var admin = ExistingAdmin();
        AddUser("alan@example.com", "Alan");

        await Seed(admin, isDevelopment: false);
        await Seed(admin, isDevelopment: false);

        Assert.Equal(1, await _context.Workspaces.CountAsync(w => w.IsPersonal, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedAsync_SkipsAnonymizedUsers()
    {
        var admin = ExistingAdmin();
        var user = AddUser("erased@example.com", "Ghost");
        user.IsAnonymized = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Seed(admin, isDevelopment: false);

        Assert.Equal(0, await _context.Workspaces.CountAsync(w => w.IsPersonal, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedAsync_WhenDevUserIsSoftDeleted_LeavesThemAloneRatherThanRecreating()
    {
        // A soft-deleted user is hidden by the query filter but still owns the unique username
        // index, so re-creating them threw 23505 and took the whole app down at startup.
        var deleted = AddUser("dev1@example.com", "Dev");
        deleted.IsDeleted = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var admin = new AdminSeedOptions { Email = "admin@acme.com", Password = "Adm1n!Secure9" };

        await Seed(admin, isDevelopment: true);

        _userManager.Verify(
            m => m.CreateAsync(It.Is<User>(u => u.Email == "dev1@example.com"), It.IsAny<string>()),
            Times.Never);
        // Deleted users get no personal workspace either.
        Assert.Equal(0, await _context.Workspaces.CountAsync(w => w.IsPersonal && w.CreatedBy == deleted.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedAsync_InDevelopment_SeedsSharedWorkspaceOnceAcrossRuns()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var admin = new AdminSeedOptions { Email = "admin@acme.com", Password = "Adm1n!Secure9" };

        await Seed(admin, isDevelopment: true);
        await Seed(admin, isDevelopment: true);

        var shared = await _context.Workspaces
            .Include(w => w.Members)
            .Where(w => !w.IsPersonal && w.Name == "Acme Team")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(shared);
        Assert.Single(shared[0].Members, m => m.Role == WorkspaceRole.Owner);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SeedAsync_WithoutAdminCredentials_ThrowsInEveryEnvironment(bool isDevelopment)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Seed(new AdminSeedOptions(), isDevelopment));
    }

    [Fact]
    public async Task SeedAsync_OutsideDevelopment_SeedsAdminAndAssignsAdminRole()
    {
        var admin = new AdminSeedOptions { Email = "admin@acme.com", Password = "Adm1n!Secure9" };
        _userManager.Setup(m => m.FindByEmailAsync(admin.Email)).ReturnsAsync((User?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), admin.Password)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        await Seed(admin, isDevelopment: false);

        _userManager.Verify(m => m.CreateAsync(It.Is<User>(u => u.Email == admin.Email), admin.Password), Times.Once);
        _userManager.Verify(m => m.AddToRoleAsync(It.Is<User>(u => u.Email == admin.Email), AppRoles.Admin), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenAdminAlreadyExists_DoesNotCreateAgain()
    {
        var admin = new AdminSeedOptions { Email = "admin@acme.com", Password = "Adm1n!Secure9" };
        _userManager.Setup(m => m.FindByEmailAsync(admin.Email))
            .ReturnsAsync(new User { Email = admin.Email, UserName = admin.Email, FirstName = "Site", LastName = "Admin" });

        await Seed(admin, isDevelopment: false);

        _userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_InDevelopment_SeedsAdminAlongsideDevData()
    {
        // Development seeds the configured admin first, then the dev dataset; mock
        // the identity calls so the dev-user seeding can complete.
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var admin = new AdminSeedOptions { Email = "admin@acme.com", Password = "Adm1n!Secure9" };

        await Seed(admin, isDevelopment: true);

        _userManager.Verify(m => m.CreateAsync(It.Is<User>(u => u.Email == admin.Email), admin.Password), Times.Once);
        _userManager.Verify(m => m.AddToRoleAsync(It.Is<User>(u => u.Email == admin.Email), AppRoles.Admin), Times.Once);
        // Dev users are also seeded (e.g. dev1@example.com).
        _userManager.Verify(m => m.CreateAsync(It.Is<User>(u => u.Email == "dev1@example.com"), It.IsAny<string>()), Times.Once);
    }

    public void Dispose() => _context.Dispose();
}
