using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed a User
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "dev_user", Email = "dev@example.com" }
        );

        // Seed some Products for that User
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Mechanical Keyboard", Price = 150.00m, UserId = 1 },
            new Product { Id = 2, Name = "Gaming Mouse", Price = 80.00m, UserId = 1 }
        );

        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }
}