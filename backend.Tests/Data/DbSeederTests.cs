using Backend.Config;
using Backend.Data;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Backend.Tests.Data;

public class DbSeederTests
{
    private readonly Mock<UserManager<User>> _userManager;
    private readonly Mock<RoleManager<IdentityRole<Guid>>> _roleManager;
    private readonly AppDbContext _context;
    private readonly ILogger _logger = new Mock<ILogger>().Object;
    private readonly ProjectRetentionOptions _retention = new() { TrashWindowDays = 30 };

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
        _context = new AppDbContext(options, new Mock<ICurrentUserService>().Object);
    }

    private Task Seed(AdminSeedOptions admin, bool isDevelopment) =>
        DbSeeder.SeedAsync(_userManager.Object, _roleManager.Object, _context, _logger, _retention, admin, isDevelopment);

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
            .ReturnsAsync(new User { Email = admin.Email, UserName = admin.Email });

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
}
