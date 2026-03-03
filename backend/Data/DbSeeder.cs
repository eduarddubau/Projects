using Backend.Models;
using Microsoft.AspNetCore.Identity;

namespace Backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(UserManager<User> userManager)
    {

        const int noOfUsers = 3;

        var seedUsers = Enumerable.Range(1, noOfUsers).Select(i => new User
        {
            UserName = $"dev{i}@example.com",
            Email = $"dev{i}@example.com",
            FirstName = "Dev",
            LastName = $"User{i}",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        }).ToList();

    foreach (var user in seedUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(user.Email!);
            
            if (existingUser == null)
            {
                var result = await userManager.CreateAsync(user, "Password123!");
                
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to seed user {user.Email}: {errors}");
                }
            }
        }
    }
}