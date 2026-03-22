using Backend.Config;
using Backend.Data;
using Backend.DTOs;
using Backend.Mappings;
using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class UserService : BaseService<User>, IUserService
{
    private readonly UserManager<User> _userManager;

    public UserService(
        AppDbContext context,
        ICurrentUserService currentUser,
        UserManager<User> userManager)
        : base(context, currentUser)
    {
        _userManager = userManager;
    }

    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync()
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .OrderByDescending(u => u.CreatedAt)
            .UserToDto()
            .ToListAsync();
    }

    public async Task<UserResponseDto?> GetAnyUserByIdAsync(Guid id)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .FirstOrDefaultAsync(u => u.Id == id);

        return user is null ? null : user.MapToDto();
    }

    public async Task<UserResponseDto> CreateUserAsync(CreateUserDto dto)
    {
        var emailExists = await _userManager.FindByEmailAsync(dto.Email) != null;
        if (emailExists)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = dto.ToEntity();

        var tempPassword = GenerateSecurePassword();
        var result = await _userManager.CreateAsync(user, tempPassword);

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, AppRoles.User);

        return user.MapToDto();
    }

    public async Task<bool> DeleteAnyUserAsync(Guid id)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<UserResponseDto?> RestoreAnyUserAsync(Guid userId)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return null;
        if (!user.IsDeleted) return user.MapToDto();

        user.IsDeleted = false;
        user.DeletedAt = null;

        await _context.SaveChangesAsync();

        return user.MapToDto();
    }

    public async Task<IEnumerable<UserResponseDto>> GetDeletedUsersAsync()
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .Where(u => u.IsDeleted)
            .OrderByDescending(u => u.DeletedAt)
            .UserToDto()
            .ToListAsync();
    }

    private static string GenerateSecurePassword()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
        return new string(Enumerable.Range(0, 16)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}