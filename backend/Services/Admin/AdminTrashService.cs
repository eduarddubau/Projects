using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Admin;

/// <summary>The soft-delete and restore behind the admin trash views. No
/// ICurrentUserService by design — an admin service reaches every row, so a caller
/// to scope by could only be a mistake.</summary>
public abstract class AdminTrashService<T>
    where T : class, IAuditEntity
{
    protected AppDbContext Context { get; }

    protected AdminTrashService(AppDbContext context)
    {
        Context = context;
    }

    protected async Task<bool> SoftDeleteByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Context
            .Set<T>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity is null)
            return false;

        // Set the flags rather than Remove(): Remove marks loaded dependents Deleted before
        // SaveChangesAsync can intercept, and dependents that aren't IAuditEntity never get
        // rescued — so a soft delete would hard-delete them depending on what was tracked.
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await Context.SaveChangesAsync(ct);

        return true;
    }

    protected async Task<T?> RestoreByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Context
            .Set<T>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity is null)
            return null;
        if (!entity.IsDeleted)
            return entity;

        entity.IsDeleted = false;
        entity.DeletedAt = null;

        await Context.SaveChangesAsync(ct);

        return entity;
    }
}
