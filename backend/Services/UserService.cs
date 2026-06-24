using Backend.Config;
using Backend.Data;
using Backend.DTOs.User;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
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

    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .OrderByDescending(u => u.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<UserResponseDto?> GetAnyUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        return user is null ? null : user.MapToDto();
    }

    public async Task<UserResponseDto> CreateUserAsync(CreateUserRequest dto, CancellationToken ct = default)
    {
        var emailExists = await _userManager.FindByEmailAsync(dto.Email) != null;
        if (emailExists)
            throw new BusinessRuleException("A user with this email already exists.");

        var user = dto.ToEntity();

        var tempPassword = GenerateSecurePassword();
        var result = await _userManager.CreateAsync(user, tempPassword);

        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join(", ", result.Errors.Select(e => e.Description)));

        await _userManager.AddToRoleAsync(user, AppRoles.User);

        return user.MapToDto();
    }

    public Task<bool> DeleteAnyUserAsync(Guid id, CancellationToken ct = default) => SoftDeleteAnyByIdAsync(id, ct);

    public async Task<UserResponseDto?> RestoreAnyUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await RestoreAnyByIdAsync(id, ct);
        if (user is null) return null;

        await _context.Entry(user).Reference(u => u.Creator).LoadAsync(ct);
        await _context.Entry(user).Reference(u => u.Updater).LoadAsync(ct);

        return user.MapToDto();
    }

    public async Task<IEnumerable<UserResponseDto>> GetDeletedUsersAsync(CancellationToken ct = default)
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .Where(u => u.IsDeleted)
            .OrderByDescending(u => u.DeletedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    private static string GenerateSecurePassword()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
        return new string(Enumerable.Range(0, 16)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }
}