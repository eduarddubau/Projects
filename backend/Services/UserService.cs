using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class UserService : BaseService<User>, IUserService
{
    public UserService(AppDbContext context, ICurrentUserService currentUser)
        : base(context, currentUser) { }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        bool emailExists = await _context.Users.AnyAsync(u => u.Email == user.Email);

        if (emailExists)
        {
            throw new InvalidOperationException("This email is already registered.");
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return false;
        }

        user.IsDeleted = true;
        await _context.SaveChangesAsync();
        return true;
    }
}