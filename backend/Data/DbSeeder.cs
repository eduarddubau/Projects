using Backend.Models;
using Backend.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        UserManager<User> userManager, 
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext context,
        ILogger logger)
    {
        logger.LogInformation("Starting database seeding...");

        // Seed Roles
        string[] roleNames = { AppRoles.Admin, AppRoles.User };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                logger.LogInformation("Creating role: {RoleName}", roleName);
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
            }
        }

        const int noOfUsers = 3;
        for (int i = 1; i <= noOfUsers; i++)
        {
            var email = $"dev{i}@example.com";
            var user = await userManager.FindByEmailAsync(email);
            
            if (user == null)
            {
                logger.LogInformation("Seeding user: {Email}", email);
                user = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = "Dev",
                    LastName = $"User{i}",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = null
                };

                var result = await userManager.CreateAsync(user, "Password123!");
                if (result.Succeeded)
                {
                    // Set CreatedBy to the user's own Id after creation to establish ownership of their data
                    user.CreatedBy = user.Id;
                    await userManager.UpdateAsync(user);
                    
                    // Assign role based on user index (first user is Admin, others are Users)
                    var role = (i == 1) ? AppRoles.Admin : AppRoles.User;
                    await userManager.AddToRoleAsync(user, role);
                }
                else 
                {
                    logger.LogError("Failed to seed user {Email}: {Errors}", 
                        email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            // Seed Projects
            var userHasProjects = await context.Projects.AnyAsync(p => p.CreatedBy == user.Id);
            
            if (!userHasProjects)
            {
                logger.LogInformation("Seeding starter projects for user: {Email}", email);
                var starterProjects = new List<Project>
                {
                    new() { 
                        Name = $"{user.FirstName}'s First Project", 
                        Description = "Automatically generated starter project.",
                        CreatedBy = user.Id,
                        IsDeleted = false
                    },
                    new() { 
                        Name = $"Ongoing Research Project no {i}", 
                        Description = "A project for tracking long-term goals.",
                        CreatedBy = user.Id,
                        IsDeleted = false
                    }
                };

                context.Projects.AddRange(starterProjects);
                await context.SaveChangesAsync();
            }
            else 
            {
                logger.LogDebug("User {Email} already has projects. Skipping project seed.", email);
            }
        }

        logger.LogInformation("Database seeding completed successfully.");
    }
}