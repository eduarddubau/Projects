using Backend.Models;
using Backend.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public static class DbSeeder
{
    private const string DefaultPassword = "Password123!";
    private const int UserCount = 3;

    public static async Task SeedAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext context,
        ILogger logger,
        ProjectRetentionOptions retentionOptions,
        AdminSeedOptions adminOptions,
        bool isDevelopment)
    {
        logger.LogInformation("Starting database seeding...");

        // Roles are required for the app to function in every environment.
        await SeedRolesAsync(roleManager, logger);

        // The configured admin is seeded in every environment so the same account
        // exists everywhere (its credentials come from configuration/secrets).
        await SeedAdminAsync(userManager, logger, adminOptions);

        if (isDevelopment)
        {
            // Development additionally gets the full demo dataset (dev users +
            // sample/trash projects) that the local app and E2E specs rely on.
            await SeedDevelopmentDataAsync(userManager, context, logger, retentionOptions);
        }

        logger.LogInformation("Database seeding completed successfully.");
    }

    private static async Task SeedDevelopmentDataAsync(
        UserManager<User> userManager,
        AppDbContext context,
        ILogger logger,
        ProjectRetentionOptions retentionOptions)
    {
        for (var index = 1; index <= UserCount; index++)
        {
            var user = await SeedUserAsync(userManager, logger, index);
            if (user is null) continue;

            await SeedProjectsForUserAsync(context, logger, user, index, retentionOptions.TrashWindowDays);
        }
    }

    private static async Task SeedAdminAsync(UserManager<User> userManager, ILogger logger, AdminSeedOptions options)
    {
        // An admin account is mandatory in every environment: fail fast so a
        // misconfigured setup is caught at startup rather than running an app
        // nobody can administer.
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException(
                $"No admin credentials configured. Set {AdminSeedOptions.SectionName}:Email and " +
                $"{AdminSeedOptions.SectionName}:Password via configuration/secrets " +
                $"(e.g. {AdminSeedOptions.SectionName}__Email / {AdminSeedOptions.SectionName}__Password).");

        var existing = await userManager.FindByEmailAsync(options.Email);
        if (existing is not null)
        {
            logger.LogInformation("Admin account {Email} already exists. Skipping admin seed.", options.Email);
            return;
        }

        logger.LogInformation("Seeding admin account: {Email}", options.Email);
        var admin = new User
        {
            UserName = options.Email,
            Email = options.Email,
            FirstName = options.FirstName,
            LastName = options.LastName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, options.Password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed admin {Email}: {Errors}",
                options.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager, ILogger logger)
    {
        foreach (var roleName in new[] { AppRoles.Admin, AppRoles.User })
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;

            logger.LogInformation("Creating role: {RoleName}", roleName);
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
        }
    }

    private static async Task<User?> SeedUserAsync(UserManager<User> userManager, ILogger logger, int index)
    {
        var email = $"dev{index}@example.com";
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null) return existingUser;

        logger.LogInformation("Seeding user: {Email}", email);
        var user = new User
        {
            UserName = email,
            Email = email,
            FirstName = "Dev",
            LastName = $"User{index}",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, DefaultPassword);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to seed user {Email}: {Errors}",
                email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return null;
        }

        var role = index == 1 ? AppRoles.Admin : AppRoles.User;
        await userManager.AddToRoleAsync(user, role);

        return user;
    }

    private static async Task SeedProjectsForUserAsync(
        AppDbContext context, ILogger logger, User user, int index, int trashWindowDays)
    {
        if (await context.Projects.AnyAsync(p => p.CreatedBy == user.Id))
        {
            logger.LogDebug("User {Email} already has projects. Skipping project seed.", user.Email);
            return;
        }

        logger.LogInformation("Seeding projects for user: {Email}", user.Email);

        var activeProjects = new[]
        {
            new Project
            {
                Name = $"{user.FirstName}'s First Project",
                Description = "Automatically generated starter project.",
                CreatedBy = user.Id
            },
            new Project
            {
                Name = $"Ongoing Research Project no {index}",
                Description = "A project for tracking long-term goals.",
                CreatedBy = user.Id
            }
        };

        // Tiered ages relative to the retention window, so the admin purge filters
        // (>30/>60/>90 days) each have something to show: one project still within
        // the window, and three past it by increasing margins.
        var deletedAges = new[]
        {
            5,
            trashWindowDays + 5,
            trashWindowDays + 35,
            trashWindowDays + 65
        };

        var deletedProjects = deletedAges.Select(ageDays => new Project
        {
            Name = $"{user.FirstName}'s Project Deleted {ageDays} Days Ago",
            Description = ageDays <= trashWindowDays
                ? "Soft-deleted recently; still within the trash retention window."
                : $"Soft-deleted {ageDays} days ago; past the {trashWindowDays}-day retention window.",
            CreatedBy = user.Id
        }).ToArray();

        context.Projects.AddRange(activeProjects);
        context.Projects.AddRange(deletedProjects);
        await context.SaveChangesAsync();

        // SaveChangesAsync forces IsDeleted = false for newly Added entities, so these
        // are seeded active first, then soft-deleted with backdated timestamps here.
        for (var i = 0; i < deletedProjects.Length; i++)
        {
            deletedProjects[i].IsDeleted = true;
            deletedProjects[i].DeletedAt = DateTime.UtcNow.AddDays(-deletedAges[i]);
        }

        await context.SaveChangesAsync();
    }
}
