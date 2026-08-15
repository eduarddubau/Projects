using System.Security.Cryptography;
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
    private readonly IWorkspaceService _workspaceService;
    private readonly ILookupNormalizer _normalizer;

    public UserService(
        AppDbContext context,
        ICurrentUserService currentUser,
        UserManager<User> userManager,
        IWorkspaceService workspaceService,
        ILookupNormalizer normalizer
    )
        : base(context, currentUser)
    {
        _userManager = userManager;
        _workspaceService = workspaceService;
        _normalizer = normalizer;
    }

    public async Task<UserResponseDto?> GetMyProfileAsync(CancellationToken ct = default)
    {
        var userId = CurrentUser.UserGuid;
        if (userId is null)
            return null;

        var user = await Context
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
        var userId = CurrentUser.UserGuid;
        if (userId is null)
            return null;

        var user = await Context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

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
        await Context.SaveChangesAsync(ct);

        return await Context.Users.Where(u => u.Id == userId).MapToDto().FirstAsync(ct);
    }

    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        return await Context
            .Users.IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .OrderByDescending(u => u.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<UserResponseDto?> GetAnyUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await Context
            .Users.IgnoreQueryFilters()
            .Include(u => u.Creator)
            .Include(u => u.Updater)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        return user?.MapToDto();
    }

    public async Task<UserResponseDto> CreateUserAsync(
        CreateUserRequest dto,
        CancellationToken ct = default
    )
    {
        var emailExists = await _userManager.FindByEmailAsync(dto.Email) != null;
        if (emailExists)
            throw new BusinessRuleException(
                BusinessRuleCodes.DuplicateEmail,
                "A user with this email already exists."
            );

        var user = dto.ToEntity();

        var tempPassword = GenerateSecurePassword();
        var result = await _userManager.CreateAsync(user, tempPassword);

        if (!result.Succeeded)
            throw new BusinessRuleException(
                BusinessRuleCodes.IdentityError,
                string.Join(", ", result.Errors.Select(e => e.Description))
            );

        await _userManager.AddToRoleAsync(user, AppRoles.User);
        await _workspaceService.EnsurePersonalWorkspaceAsync(user, ct);

        return user.MapToDto();
    }

    public Task<bool> DeleteAnyUserAsync(Guid id, CancellationToken ct = default) =>
        SoftDeleteAnyByIdAsync(id, ct);

    public async Task<UserResponseDto?> RestoreAnyUserAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await Context
            .Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (deleted is null)
            return null;

        if (deleted.IsDeleted)
        {
            // Uniqueness is scoped to live rows, so restoring re-enters the partial index.
            // Only a live holder blocks it — another deleted row isn't in the index either.
            // Email only: UserName is derived from the row's own id, so it cannot collide.
            bool taken = await Context.Users.AnyAsync(
                u => u.Id != id && u.NormalizedEmail == deleted.NormalizedEmail,
                ct
            );

            if (taken)
                throw new BusinessRuleException(
                    BusinessRuleCodes.EmailReclaimed,
                    $"{deleted.Email} now belongs to another account. Erase this one, or have the current holder change their address first.",
                    new Dictionary<string, string> { ["email"] = deleted.Email ?? string.Empty }
                );

            deleted.IsDeleted = false;
            deleted.DeletedAt = null;
            await Context.SaveChangesAsync(ct);
        }

        return await Context.Users.Where(u => u.Id == id).MapToDto().FirstAsync(ct);
    }

    public async Task<IEnumerable<UserResponseDto>> GetDeletedUsersAsync(
        CancellationToken ct = default
    )
    {
        return await Context
            .Users.IgnoreQueryFilters()
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
        var user = await Context
            .Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted && !u.IsAnonymized, ct);

        if (user is null)
            return false;

        var soleOwnedNames = await Context
            .Workspaces.Where(w =>
                !w.IsPersonal
                && w.Members.Any(m => m.UserId == id && m.Role == WorkspaceRole.Owner)
                && !w.Members.Any(m => m.UserId != id && m.Role == WorkspaceRole.Owner)
            )
            .OrderBy(w => w.Name)
            .Select(w => w.Name)
            .ToListAsync(ct);

        if (soleOwnedNames.Count > 0)
        {
            var names = string.Join(", ", soleOwnedNames);

            throw new BusinessRuleException(
                BusinessRuleCodes.SoleOwnerOfWorkspaces,
                $"This user is the only owner of: {names}. Promote another owner or delete those workspaces first.",
                new Dictionary<string, string> { ["workspaces"] = names }
            );
        }

        var personalWorkspaces = await Context
            .Workspaces.Where(w => w.IsPersonal && w.Members.Any(m => m.UserId == id))
            .ToListAsync(ct);

        var memberships = await Context.WorkspaceMembers.Where(m => m.UserId == id).ToListAsync(ct);

        // A real delete: the user stops appearing in member lists rather than lingering
        // as "Deleted User".
        Context.WorkspaceMembers.RemoveRange(memberships);

        var now = DateTime.UtcNow;

        // A soft delete: the personal workspace is hidden from the user and cannot be restored.
        foreach (var workspace in personalWorkspaces)
        {
            workspace.IsDeleted = true;
            workspace.DeletedAt = now;
        }

        var tombstone = $"deleted-{user.Id:N}@anonymized.invalid";

        user.FirstName = "Deleted";
        user.LastName = "User";
        user.Nickname = null;
        user.Email = tombstone;
        user.NormalizedEmail = _normalizer.NormalizeEmail(tombstone);
        user.PhoneNumber = null;
        user.PasswordHash = null;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.IsAnonymized = true;
        user.AnonymizedAt = now;

        await Context.SaveChangesAsync(ct);

        return true;
    }

    private static string GenerateSecurePassword()
    {
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string symbols = "!@#$%^&*";
        const string all = lower + upper + digits + symbols;

        // RandomNumberGenerator, never Random.Shared: this value is a live
        // credential, and Random is a seeded PRNG whose sequence can be inferred
        // from other draws.
        // Guarantee one character from each class Identity's password policy
        // requires, then fill the remaining length from the full pool. Without
        // this, a random draw can omit a class and Identity rejects the password.
        var password = new List<char>
        {
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)],
        };

        password.AddRange(
            Enumerable.Range(0, 12).Select(_ => all[RandomNumberGenerator.GetInt32(all.Length)])
        );

        // Shuffle so the guaranteed characters aren't always in the same positions.
        for (int i = password.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string([.. password]);
    }
}
