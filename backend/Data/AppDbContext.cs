using Backend.Services.Interfaces;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Backend.Config;
using Backend.Exceptions;

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
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<WorkspaceMember> WorkspaceMembers { get; set; }
    public DbSet<Invitation> Invitations { get; set; }

    // Bypasses the soft-delete interception below for this entity's next removal.
    public void MarkForHardDelete(object entity) => _hardDeleteOverrides.Add(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);
        modelBuilder.Entity<Workspace>().HasQueryFilter(w => !w.IsDeleted);

        modelBuilder.Entity<User>(entity =>
        {
            // Uniqueness scoped to live rows: a soft-deleted account stops reserving its address.
            // Postgres enforces this atomically, which no validator can — Identity's checks read
            // through the !IsDeleted filter and aren't atomic with the insert anyway.
            entity.HasIndex(u => u.NormalizedUserName)
                .IsUnique()
                .HasFilter("is_deleted = false");

            entity.HasIndex(u => u.NormalizedEmail)
                .IsUnique()
                .HasFilter("is_deleted = false");

            entity.HasOne(u => u.Creator)
                .WithMany()
                .HasForeignKey(u => u.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(u => u.Updater)
                .WithMany()
                .HasForeignKey(u => u.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.Property(u => u.FirstName).HasMaxLength(50);

            entity.Property(u => u.LastName).HasMaxLength(50);
            
            entity.Property(u => u.Nickname).HasMaxLength(30);
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

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasOne(w => w.Creator)
                .WithMany()
                .HasForeignKey(w => w.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(w => w.Updater)
                .WithMany()
                .HasForeignKey(w => w.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(w => w.Name).HasMaxLength(Workspace.NameMaxLength);

            entity.Property(w => w.Description).HasMaxLength(Workspace.DescriptionMaxLength);

            // Project.WorkspaceId doesn't exist until later — without this,
            // EF's convention discovery invents a shadow FK on projects right now instead.
            entity.Ignore(w => w.Projects);
        });

        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();

            entity.Property(m => m.Role).HasConversion<string>();

            entity.HasOne(m => m.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(m => m.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasIndex(i => new { i.WorkspaceId, i.NormalizedEmail });
            entity.HasIndex(i => i.TokenHash).IsUnique();

            entity.Property(i => i.Role).HasConversion<string>();

            entity.HasOne(i => i.Workspace)
                .WithMany()
                .HasForeignKey(i => i.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, like the audit FKs: an inviter must not be purged out from under
            // the invitations that record what they did.
            entity.HasOne(i => i.Inviter)
                .WithMany()
                .HasForeignKey(i => i.InvitedBy)
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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
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

                // Preserves a CreatedBy the caller set deliberately; only stamps when unset.
                if (entityEntry.Entity.CreatedBy == null || entityEntry.Entity.CreatedBy == Guid.Empty)
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
            // A safety net, not the mechanism. Services soft-delete by setting the flags
            // themselves, because Remove() cascades to loaded dependents before this runs and
            // ones that aren't IAuditEntity are never rescued below. Kept so a stray Remove()
            // hides a row instead of destroying it.
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

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // A unique index is the only check that's atomic with the insert, so it catches
            // races no service-layer guard can. Translated here because this is the last
            // place that legitimately knows what database we're on.
            throw pg.ConstraintName switch
            {
                "UserNameIndex" or "EmailIndex" => new BusinessRuleException(
                    BusinessRuleCodes.DuplicateEmail, "That email address is already registered."),
                _ => ex
            };
        }
    }
}
