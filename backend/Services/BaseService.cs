using Backend.Data;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Shared soft-delete and restore for entities an admin manages through
/// the trash views. Inherit only to reuse those — services that merely need the
/// context should take it directly.</summary>
public abstract class BaseService<T> where T : class, IAuditEntity
{
    protected readonly AppDbContext _context;
    protected readonly ICurrentUserService _currentUser;

    protected BaseService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    protected async Task<bool> SoftDeleteAnyByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Set<T>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity is null) return false;

        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync(ct);

        return true;
    }

    protected async Task<T?> RestoreAnyByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Set<T>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity is null) return null;
        if (!entity.IsDeleted) return entity;

        entity.IsDeleted = false;
        entity.DeletedAt = null;

        await _context.SaveChangesAsync(ct);

        return entity;
    }
}
