using backend.Services;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Backend.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUserService _currentUserService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        modelBuilder.Entity<User>()
            .Property(u => u.Id)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? "System";
        var now = DateTime.UtcNow;

        var entries = ChangeTracker.Entries<IAuditEntity>()
        .Where(e => e.State == EntityState.Added || 
                    e.State == EntityState.Modified || 
                    e.State == EntityState.Deleted);

        foreach (var entityEntry in entries)
        {
            if (entityEntry.State == EntityState.Added)
            {
                entityEntry.Entity.CreatedAt = now;
                entityEntry.Entity.CreatedBy = userId?? "System";
                entityEntry.Entity.IsDeleted = false;
            }
            else if (entityEntry.State == EntityState.Modified)
            {
                // Prevent original audit data from being overwritten
                entityEntry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                entityEntry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;

                entityEntry.Entity.UpdatedAt = now;
                entityEntry.Entity.UpdatedBy = userId ?? "System";
            }
            else if (entityEntry.State == EntityState.Deleted)
            {
                entityEntry.State = EntityState.Modified;
                entityEntry.Entity.IsDeleted = true;
                entityEntry.Entity.DeletedAt = now;
                entityEntry.Entity.UpdatedBy = userId ?? "System";
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}