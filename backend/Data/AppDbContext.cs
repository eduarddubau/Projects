using Backend.Services.Interfaces;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Backend.Data;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly HashSet<object> _hardDeleteOverrides = new();

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Project> Projects { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // Bypasses the soft-delete interception below for this entity's next removal.
    public void MarkForHardDelete(object entity) => _hardDeleteOverrides.Add(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);

        modelBuilder.Entity<User>(entity =>
        {
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
            entity.HasOne(p => p.Creator)
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Updater)
                .WithMany()
                .HasForeignKey(p => p.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(rt => rt.TokenHash).IsUnique();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var userGuid = _currentUserService.UserGuid;
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
                entityEntry.Entity.IsDeleted = false;

                if (entityEntry.Entity.CreatedBy == null || entityEntry.Entity.CreatedBy == Guid.Empty) // FIX
                {
                    entityEntry.Entity.CreatedBy = entityEntry.Entity is User newUser
                        ? newUser.Id
                        : userGuid;
                }
            }
            else if (entityEntry.State == EntityState.Modified)
            {
                entityEntry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                entityEntry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;

                entityEntry.Entity.UpdatedAt = now;

                if (userGuid.HasValue)
                    entityEntry.Entity.UpdatedBy = userGuid;
            }
            else if (entityEntry.State == EntityState.Deleted)
            {
                if (_hardDeleteOverrides.Remove(entityEntry.Entity))
                    continue;

                entityEntry.State = EntityState.Modified;
                entityEntry.Entity.IsDeleted = true;
                entityEntry.Entity.DeletedAt = now;
                entityEntry.Entity.UpdatedAt = now;

                if (userGuid.HasValue)
                    entityEntry.Entity.UpdatedBy = userGuid;

                entityEntry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                entityEntry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}