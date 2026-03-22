using Backend.Services;
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

    public DbSet<Project> Projects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filters to exclude soft-deleted entities
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);

        // Configure UUID generation for primary keys and relationships for audit fields
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(u => u.Creator)
                .WithMany() 
                .HasForeignKey(u => u.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.Updater)
                .WithMany()
                .HasForeignKey(u => u.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(p => p.Id)
                .ValueGeneratedOnAdd()
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(p => p.Creator)
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Updater)
                .WithMany()
                .HasForeignKey(p => p.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Get the current user's GUID for audit purposes
        var userGuid = _currentUserService.UserGuid;
        var now = DateTime.UtcNow;

        // Process all entities that implement IAuditEntity and are being added, modified, or deleted
        var entries = ChangeTracker.Entries<IAuditEntity>()
        .Where(e => e.State == EntityState.Added || 
                    e.State == EntityState.Modified || 
                    e.State == EntityState.Deleted);

        // Handle audit fields based on the entity state
        foreach (var entityEntry in entries)
        {
            if (entityEntry.State == EntityState.Added)
            {
                entityEntry.Entity.CreatedAt = now;

                // If CreatedBy is not set, assign the current user's GUID
                if (entityEntry.Entity.CreatedBy == Guid.Empty || entityEntry.Entity.CreatedBy == null)
                {
                    entityEntry.Entity.CreatedBy = userGuid;
                }
                entityEntry.Entity.IsDeleted = false;
            }
            else if (entityEntry.State == EntityState.Modified)
            {
                // Prevent original audit data from being overwritten
                entityEntry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                entityEntry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;

                entityEntry.Entity.UpdatedAt = now;
                entityEntry.Entity.UpdatedBy = userGuid;
            }
            else if (entityEntry.State == EntityState.Deleted)
            {
                entityEntry.State = EntityState.Modified;
                entityEntry.Entity.IsDeleted = true;
                entityEntry.Entity.DeletedAt = now;
                entityEntry.Entity.UpdatedBy = userGuid;

                // Prevent original audit data from being overwritten
                entityEntry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                entityEntry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}