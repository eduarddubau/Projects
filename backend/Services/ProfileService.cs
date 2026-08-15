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

/// <summary>The signed-in account, and nothing else.</summary>
public class ProfileService : IProfileService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<User> _userManager;
    private readonly ILookupNormalizer _normalizer;

    public ProfileService(
        AppDbContext context,
        ICurrentUserService currentUser,
        UserManager<User> userManager,
        ILookupNormalizer normalizer
    )
    {
        _context = context;
        _currentUser = currentUser;
        _userManager = userManager;
        _normalizer = normalizer;
    }

    public async Task<UserResponseDto?> GetMyProfileAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserGuid;
        if (userId is null)
            return null;

        var user = await _context
            .Users.Include(u => u.Creator)
            .Include(u => u.Updater)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return user?.MapToDto();
    }

    public async Task<UserResponseDto?> UpdateMyProfileAsync(
        UpdateProfileRequest dto,
        CancellationToken ct = default
    )
    {
        var userId = _currentUser.UserGuid;
        if (userId is null)
            return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return null;

        // Names first, without saving: SetEmailAsync calls UpdateAsync, which saves the whole
        // tracked context — so these commit in the same write. Reversing the order gives two
        // saves with a window where the email moved but the name didn't.
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Nickname = dto.Nickname;

        // Compare normalized, or "ada@x.com" -> "Ada@X.com" reads as a change and needlessly
        // resets EmailConfirmed. UserName is no longer a copy of Email, so this moves one column.
        if (user.NormalizedEmail != _normalizer.NormalizeEmail(dto.Email))
        {
            var result = await _userManager.SetEmailAsync(user, dto.Email);

            // A live holder is caught here by Identity's validator; a concurrent race is caught
            // by the partial unique index and translated in AppDbContext.SaveChangesAsync.
            if (!result.Succeeded)
                throw new BusinessRuleException(
                    BusinessRuleCodes.DuplicateEmail,
                    string.Join(", ", result.Errors.Select(e => e.Description))
                );
        }

        // No-op when SetEmailAsync already saved; needed when only the name changed.
        await _context.SaveChangesAsync(ct);

        return await _context.Users.Where(u => u.Id == userId).MapToDto().FirstAsync(ct);
    }
}
