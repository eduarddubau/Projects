using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Backend.Services.Interfaces;

namespace Backend.Services;

public enum FetchResult { NotFound, Forbidden, Success }

public abstract class BaseService<T> where T : class, IAuditEntity
{
    protected readonly AppDbContext _context;
    protected readonly ICurrentUserService _currentUser;

    protected BaseService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    protected async Task<(FetchResult Result, T? Entity)> GetByIdSecureAsync(Guid id)
    {
        var entity = await _context.Set<T>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (entity is null)
            return (FetchResult.NotFound, null);

        if (_currentUser.IsAdmin || entity.CreatedBy == _currentUser.UserGuid)
            return (FetchResult.Success, entity);

        return (FetchResult.Forbidden, null);
    }
}