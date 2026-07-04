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

    public async Task<UserResponseDto?> GetMyProfileAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserGuid;
        if (userId is null) return null;

        var user = await _context.Users
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return user?.MapToDto();
    }

    public async Task<UserResponseDto?> UpdateMyProfileAsync(UpdateProfileRequest dto, CancellationToken ct = default)
    {
        var userId = _currentUser.UserGuid;
        if (userId is null) return null;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return null;

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;

        await _context.SaveChangesAsync(ct);

        await _context.Entry(user).Reference(u => u.Creator).LoadAsync(ct);
        await _context.Entry(user).Reference(u => u.Updater).LoadAsync(ct);

        return user.MapToDto();
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
            .Where(u => u.IsDeleted && !u.IsAnonymized)
            .OrderByDescending(u => u.DeletedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Irreversibly scrubs a deleted user's personal data (GDPR erasure) while keeping the row,
    /// so audit foreign keys referencing them stay valid. The account is hidden from the trash
    /// afterwards and cannot be restored.
    /// </summary>
    public async Task<bool> AnonymizeUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted && !u.IsAnonymized, ct);

        if (user is null) return false;

        var tombstone = $"deleted-{user.Id:N}@anonymized.invalid";

        user.FirstName = "Deleted";
        user.LastName = "User";
        user.Email = tombstone;
        user.NormalizedEmail = tombstone.ToUpperInvariant();
        user.UserName = tombstone;
        user.NormalizedUserName = tombstone.ToUpperInvariant();
        user.PhoneNumber = null;
        user.PasswordHash = null;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.IsAnonymized = true;
        user.AnonymizedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return true;
    }

    private static string GenerateSecurePassword()
    {
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string symbols = "!@#$%^&*";
        const string all = lower + upper + digits + symbols;

        // Guarantee one character from each class Identity's password policy
        // requires, then fill the remaining length from the full pool. Without
        // this, a random draw can omit a class and Identity rejects the password.
        var password = new List<char>
        {
            lower[Random.Shared.Next(lower.Length)],
            upper[Random.Shared.Next(upper.Length)],
            digits[Random.Shared.Next(digits.Length)],
            symbols[Random.Shared.Next(symbols.Length)]
        };

        password.AddRange(Enumerable.Range(0, 12)
            .Select(_ => all[Random.Shared.Next(all.Length)]));

        // Shuffle so the guaranteed characters aren't always in the same positions.
        for (int i = password.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password.ToArray());
    }
}