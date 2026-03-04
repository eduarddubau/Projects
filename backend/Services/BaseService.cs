using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public abstract class BaseService<T> where T : class, IAuditEntity
{
    protected readonly AppDbContext _context;
    protected readonly ICurrentUserService _currentUser;

    protected BaseService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    protected async Task<T?> GetByIdSecureAsync(Guid id)
    {
        var entity = await _context.Set<T>().FindAsync(id);
        
        if (entity == null) return null;

        if (_currentUser.IsAdmin || entity.CreatedBy == _currentUser.UserId)
        {
            return entity;
        }

        throw new UnauthorizedAccessException("You don't have permission to access this resource.");
    }
}