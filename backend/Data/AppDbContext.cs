using backend.Services;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }
    
    
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user1Id = Guid.Parse("018e1a2b-3c4d-7000-8000-000000000001");
        var user2Id = Guid.Parse("018e1a2b-3c4d-7000-8000-000000000002");
        var product1Id = Guid.Parse("018e1a2b-3c4d-7000-8000-000000000011");
        var product2Id = Guid.Parse("018e1a2b-3c4d-7000-8000-000000000012");
        var product3Id = Guid.Parse("018e1a2b-3c4d-7000-8000-000000055555");
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<User>().HasData(
            new User { Id = user1Id, Username = "dev_user1", Email = "dev1@example.com", CreatedAt = seedDate, CreatedBy = "System" },
            new User { Id = user2Id, Username = "dev_user2", Email = "dev2@example.com", CreatedAt = seedDate, CreatedBy = "System" }
        );

        modelBuilder.Entity<User>()
            .Property(u => u.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? "System";
        var now = DateTime.UtcNow;

        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is BaseEntity && (
                e.State == EntityState.Added || 
                e.State == EntityState.Modified || 
                e.State == EntityState.Deleted));

        foreach (var entityEntry in entries)
        {
            var entity = (BaseEntity)entityEntry.Entity;

            if (entityEntry.State == EntityState.Added)
            {
                entity.CreatedAt = now;
                entity.CreatedBy = userId?? "System";
                entity.IsDeleted = false;
            }
            else if (entityEntry.State == EntityState.Modified)
            {
                // Prevent original audit data from being overwritten
                entityEntry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                entityEntry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;

                entity.UpdatedAt = now;
                entity.UpdatedBy = userId ?? "System";
            }
            else if (entityEntry.State == EntityState.Deleted)
            {
                entityEntry.State = EntityState.Modified;
                entity.IsDeleted = true;
                entity.DeletedAt = now;
                entity.UpdatedBy = userId ?? "System";
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}