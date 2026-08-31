using System.Security.Cryptography;
using Backend.Config;
using Backend.Data;
using Backend.DTOs.User;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Admin.Interfaces;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Admin;

public class AdminUserService : IAdminUserService
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IWorkspaceService _workspaceService;
    private readonly ILookupNormalizer _normalizer;

    public AdminUserService(
        AppDbContext context,
        UserManager<User> userManager,
        IWorkspaceService workspaceService,
        ILookupNormalizer normalizer
    )
    {
        _context = context;
        _userManager = userManager;
        _workspaceService = workspaceService;
        _normalizer = normalizer;
    }

    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        return await _context
            .Users.IgnoreQueryFilters()
            .OrderByDescending(u => u.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<UserResponseDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context
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

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _context
            .Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
            return false;

        // Set the flags rather than Remove(): Remove marks loaded dependents Deleted before
        // SaveChangesAsync can intercept, and dependents that aren't IAuditEntity never get
        // rescued — so a soft delete would hard-delete them depending on what was tracked.
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<UserResponseDto?> RestoreUserAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await _context
            .Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (deleted is null)
            return null;

        if (deleted.IsDeleted)
        {
            // Uniqueness is scoped to live rows, so restoring re-enters the partial index.
            // Only a live holder blocks it — another deleted row isn't in the index either.
            // Email only: UserName is derived from the row's own id, so it cannot collide.
            bool taken = await _context.Users.AnyAsync(
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
            await _context.SaveChangesAsync(ct);
        }

        return await _context.Users.Where(u => u.Id == id).MapToDto().FirstAsync(ct);
    }

    public async Task<IEnumerable<UserResponseDto>> GetDeletedUsersAsync(
        CancellationToken ct = default
    )
    {
        return await _context
            .Users.IgnoreQueryFilters()
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
        var user = await _context
            .Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted && !u.IsAnonymized, ct);

        if (user is null)
            return false;

        var soleOwnedNames = await _context
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

        var personalWorkspaces = await _context
            .Workspaces.IgnoreQueryFilters()
            .Where(w => w.IsPersonal && w.Members.Any(m => m.UserId == id))
            .ToListAsync(ct);

        var memberships = await _context
            .WorkspaceMembers.Where(m => m.UserId == id)
            .ToListAsync(ct);

        // A real delete: the user stops appearing in member lists rather than lingering
        // as "Deleted User".
        _context.WorkspaceMembers.RemoveRange(memberships);

        var now = DateTime.UtcNow;

        await ErasePersonalWorkspacesAsync(personalWorkspaces, ct);

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

        await _context.SaveChangesAsync(ct);

        return true;
    }

    /// <summary>
    /// Destroys the erased account's private workspaces outright — their projects and tasks
    /// first, since both FKs are Restrict.
    /// </summary>
    // Not a soft delete. The workspace's name is derived from the user's own
    // (WorkspaceService.PersonalWorkspaceName), so a surviving row keeps the name erasure
    // exists to remove and shows it in the admin workspace trash. Soft-deleting also left
    // the projects inside untouched and therefore reachable by nothing, while
    // PurgeWorkspacesAsync counts them through IgnoreQueryFilters and refuses the workspace
    // forever. Nothing else may reference these rows: the workspace is private by
    // definition, and its members and invitations cascade.
    private async Task ErasePersonalWorkspacesAsync(
        List<Workspace> workspaces,
        CancellationToken ct
    )
    {
        if (workspaces.Count == 0)
            return;

        var workspaceIds = workspaces.ConvertAll(w => w.Id);

        var projects = await _context
            .Projects.IgnoreQueryFilters()
            .Where(p => workspaceIds.Contains(p.WorkspaceId))
            .ToListAsync(ct);

        var projectIds = projects.ConvertAll(p => p.Id);

        // IgnoreQueryFilters throughout: a soft-deleted row holds its parent's FK just as
        // hard as a live one.
        var tasks = await _context
            .Tasks.IgnoreQueryFilters()
            .Where(t => projectIds.Contains(t.ProjectId))
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            _context.MarkForHardDelete(task);
            _context.Tasks.Remove(task);
        }

        foreach (var project in projects)
        {
            _context.MarkForHardDelete(project);
            _context.Projects.Remove(project);
        }

        foreach (var workspace in workspaces)
        {
            _context.MarkForHardDelete(workspace);
            _context.Workspaces.Remove(workspace);
        }
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
