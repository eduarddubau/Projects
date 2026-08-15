using Backend.Config;
using Backend.Data;
using Backend.DTOs.Workspace;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Security;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class InvitationService : IInvitationService
{
    private const int InvitationLifetimeDays = 14;

    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAccessService _accessService;
    private readonly ILookupNormalizer _normalizer;

    public InvitationService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAccessService accessService,
        ILookupNormalizer normalizer
    )
    {
        _context = context;
        _currentUser = currentUser;
        _accessService = accessService;
        _normalizer = normalizer;
    }

    public async Task<InviteResultDto> InviteAsync(
        Guid workspaceId,
        InviteRequest dto,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(workspaceId, ct);

        var workspace =
            await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNoMembers,
                "A personal workspace cannot have other members."
            );

        // Ask Identity how it normalizes rather than reimplementing it: the normalizer
        // is swappable in IdentityOptions, and a hardcoded ToUpperInvariant would
        // silently stop matching NormalizedEmail the day anyone changes it.
        var normalized = _normalizer.NormalizeEmail(dto.Email);

        // IgnoreQueryFilters: a soft-deleted user is invisible to the !IsDeleted filter but still
        // holds the unique username index, so they can never re-register — an invite to that
        // address could never be redeemed.
        var existing = await _context
            .Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);

        if (existing is { IsDeleted: true })
            throw new BusinessRuleException(
                BusinessRuleCodes.EmailBelongsToDeletedAccount,
                "This address belongs to a deleted account and cannot be invited."
            );

        if (existing is not null && await _context.IsAdminAsync(existing.Id, ct))
            throw new BusinessRuleException(
                BusinessRuleCodes.AdminCannotJoinWorkspace,
                "Administrator accounts cannot join workspaces."
            );

        var role = dto.Role ?? WorkspaceRole.Member;

        return existing is not null
            ? await JoinExistingUserAsync(workspaceId, existing, role, ct)
            : await CreatePendingInvitationAsync(workspaceId, dto, normalized, role, ct);
    }

    private async Task<InviteResultDto> JoinExistingUserAsync(
        Guid workspaceId,
        User user,
        WorkspaceRole role,
        CancellationToken ct
    )
    {
        bool alreadyMember = await _context.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == workspaceId && m.UserId == user.Id,
            ct
        );

        if (alreadyMember)
            throw new BusinessRuleException(
                BusinessRuleCodes.AlreadyWorkspaceMember,
                "This user is already a member of the workspace."
            );

        _context.WorkspaceMembers.Add(
            new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = user.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow,
            }
        );

        await _context.SaveChangesAsync(ct);

        var member = await _context
            .WorkspaceMembers.Where(m => m.WorkspaceId == workspaceId && m.UserId == user.Id)
            .MapToDto()
            .FirstAsync(ct);

        return new InviteResultDto(InviteOutcome.Joined, null, member);
    }

    private async Task<InviteResultDto> CreatePendingInvitationAsync(
        Guid workspaceId,
        InviteRequest dto,
        string normalized,
        WorkspaceRole role,
        CancellationToken ct
    )
    {
        bool pendingExists = await _context
            .Invitations.Where(i => i.WorkspaceId == workspaceId && i.NormalizedEmail == normalized)
            .Pending()
            .AnyAsync(ct);

        if (pendingExists)
            throw new BusinessRuleException(
                BusinessRuleCodes.PendingInvitationExists,
                "This address already has a pending invitation to this workspace."
            );

        var (raw, hash) = SecureToken.Generate();
        var now = DateTime.UtcNow;

        // Not an IAuditEntity, so SaveChangesAsync stamps nothing here.
        _context.Invitations.Add(
            new Invitation
            {
                WorkspaceId = workspaceId,
                Email = dto.Email,
                NormalizedEmail = normalized,
                Role = role,
                TokenHash = hash,
                InvitedBy = RequireCurrentUserId(),
                CreatedAt = now,
                ExpiresAt = now.AddDays(InvitationLifetimeDays),
            }
        );

        await _context.SaveChangesAsync(ct);

        return new InviteResultDto(InviteOutcome.Invited, raw, null);
    }

    public async Task<IEnumerable<InvitationResponseDto>> GetPendingAsync(
        Guid workspaceId,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(workspaceId, ct);

        return await _context
            .Invitations.Where(i => i.WorkspaceId == workspaceId)
            .Pending()
            .OrderByDescending(i => i.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task RevokeAsync(
        Guid workspaceId,
        Guid invitationId,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(workspaceId, ct);

        // WorkspaceId belongs in the predicate, not just the guard: without it an owner of one
        // workspace could revoke another workspace's invitation by id.
        var invitation =
            await _context.Invitations.FirstOrDefaultAsync(
                i => i.Id == invitationId && i.WorkspaceId == workspaceId,
                ct
            ) ?? throw new NotFoundException("Invitation not found.");

        // The entity is materialised, so the computed property is the right tool here —
        // contrast .Pending() above, which has to run in SQL.
        if (!invitation.IsPending)
            throw new BusinessRuleException(
                BusinessRuleCodes.InvitationInvalid,
                "This invitation is no longer pending."
            );

        invitation.RevokedAt = DateTime.UtcNow;
        invitation.RevokedBy = RequireCurrentUserId();

        await _context.SaveChangesAsync(ct);
    }

    public async Task<WorkspaceResponseDto> AcceptAsync(
        string rawToken,
        CancellationToken ct = default
    )
    {
        var userId = RequireCurrentUserId();
        var hash = SecureToken.Hash(rawToken);

        // Deliberately not .Pending(): an expired or revoked invite has to stay distinguishable
        // from a token that never existed, or the message can't be truthful.
        var invitation =
            await _context.Invitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct)
            ?? throw new BusinessRuleException(
                BusinessRuleCodes.InvitationInvalid,
                "This invitation link is not valid."
            );

        if (!invitation.IsPending)
            throw new BusinessRuleException(
                BusinessRuleCodes.InvitationInvalid,
                "This invitation link has expired or been revoked."
            );

        // Filtered on purpose: a valid token to a trashed workspace must not work.
        _ =
            await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == invitation.WorkspaceId, ct)
            ?? throw new NotFoundException("Workspace not found.");

        // Bearer semantics: whoever holds the link may redeem it. The caller's email is
        // deliberately NOT compared against invitation.Email — see the build guide.
        bool alreadyMember = await _context.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == invitation.WorkspaceId && m.UserId == userId,
            ct
        );

        if (alreadyMember)
            throw new BusinessRuleException(
                BusinessRuleCodes.AlreadyWorkspaceMember,
                "You are already a member of this workspace."
            );

        _context.WorkspaceMembers.Add(
            new WorkspaceMember
            {
                WorkspaceId = invitation.WorkspaceId,
                UserId = userId,
                Role = invitation.Role,
                JoinedAt = DateTime.UtcNow,
            }
        );

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedBy = userId;

        // One save: split it in two and a crash between them leaves the invitation marked
        // accepted with no membership behind it.
        await _context.SaveChangesAsync(ct);

        return await _context
            .Workspaces.Where(w => w.Id == invitation.WorkspaceId)
            .MapToDto(userId)
            .FirstAsync(ct);
    }

    /// <summary>
    /// The no-token path, called from registration. Two things separate it from
    /// <see cref="AcceptAsync"/>: there is no authenticated user yet, so it works off
    /// <paramref name="user"/> rather than ICurrentUserService; and nothing here may throw,
    /// because a bad invitation must not fail someone's signup.
    /// </summary>
    public async Task RedeemPendingForEmailAsync(User user, CancellationToken ct = default)
    {
        var normalized = user.NormalizedEmail ?? _normalizer.NormalizeEmail(user.Email);
        if (normalized is null)
            return;

        var invitations = await _context
            .Invitations.Where(i => i.NormalizedEmail == normalized)
            .Pending()
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);

        if (invitations.Count == 0)
            return;

        var workspaceIds = invitations.Select(i => i.WorkspaceId).Distinct().ToList();

        // Live workspaces the user isn't already in. Everything excluded here is skipped
        // silently rather than refused — this is reconciliation, not a user action.
        var joinableIds = await _context
            .Workspaces.Where(w => workspaceIds.Contains(w.Id))
            .Where(w => !w.Members.Any(m => m.UserId == user.Id))
            .Select(w => w.Id)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var joined = new HashSet<Guid>();

        foreach (var invitation in invitations)
        {
            // Every pending invite for this address is consumed, even the ones that don't
            // produce a membership, so none is left dangling for a later redeem.
            invitation.AcceptedAt = now;
            invitation.AcceptedBy = user.Id;

            // joined guards the (WorkspaceId, UserId) unique index against two pending
            // invites to the same workspace.
            if (
                !joinableIds.Contains(invitation.WorkspaceId) || !joined.Add(invitation.WorkspaceId)
            )
                continue;

            _context.WorkspaceMembers.Add(
                new WorkspaceMember
                {
                    WorkspaceId = invitation.WorkspaceId,
                    UserId = user.Id,
                    Role = invitation.Role,
                    JoinedAt = now,
                }
            );
        }

        await _context.SaveChangesAsync(ct);
    }

    private Guid RequireCurrentUserId() =>
        _currentUser.UserGuid ?? throw new UnauthorizedAccessException("No authenticated user.");
}
