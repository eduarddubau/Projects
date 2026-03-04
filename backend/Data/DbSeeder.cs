using Backend.Models;
using Backend.Config; // To access AppRoles
using Microsoft.AspNetCore.Identity;

namespace Backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        UserManager<User> userManager, 
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        // Seed Roles first
        string[] roleNames = { AppRoles.Admin, AppRoles.User };
        foreach (var roleName in roleNames)
        {
            var roleExist = await roleManager.RoleExistsAsync(roleName);
            if (!roleExist)
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
            }
        }

        // Seed Users
        const int noOfUsers = 3;
        for (int i = 1; i <= noOfUsers; i++)
        {
            var email = $"dev{i}@example.com";
            var existingUser = await userManager.FindByEmailAsync(email);
            
            if (existingUser == null)
            {
                var user = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = "Dev",
                    LastName = $"User{i}",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                var result = await userManager.CreateAsync(user, "Password123!");
                
                if (result.Succeeded)
                {
                    var roleToAssign = AppRoles.User;
                    if (i == 1)
                    {
                        roleToAssign = AppRoles.Admin;
                    }

                    Console.WriteLine($"Seeding user: {email} with role: {roleToAssign}");
                    await userManager.AddToRoleAsync(user, roleToAssign);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to seed user {email}: {errors}");
                }
            }
        }
    }
}