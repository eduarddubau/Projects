using Backend.Config;
using Backend.Data;
using Backend.DTOs.Workspace;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Workspaces the caller belongs to.</summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAccessService _accessService;

    public WorkspaceService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAccessService accessService
    )
    {
        _context = context;
        _currentUser = currentUser;
        _accessService = accessService;
    }

    public async Task<IEnumerable<WorkspaceResponseDto>> GetMyWorkspacesAsync(
        CancellationToken ct = default
    )
    {
        return await _context
            .Workspaces.Where(w => w.Members.Any(m => m.UserId == _currentUser.UserGuid))
            .OrderByDescending(w => w.IsPersonal)
            .ThenBy(w => w.Name)
            .MapToDto(_currentUser.UserGuid)
            .ToListAsync(ct);
    }

    public async Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        return await _context
            .Workspaces.Where(w =>
                w.Id == id && w.Members.Any(m => m.UserId == _currentUser.UserGuid)
            )
            .MapToDto(_currentUser.UserGuid)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<WorkspaceResponseDto> CreateWorkspaceAsync(
        CreateWorkspaceRequest dto,
        CancellationToken ct = default
    )
    {
        var userId = RequireCurrentUserId();

        var workspace = new Workspace
        {
            Name = dto.Name,
            Description = dto.Description,
            IsPersonal = false,
            Members =
            {
                new WorkspaceMember
                {
                    UserId = userId,
                    Role = WorkspaceRole.Owner,
                    JoinedAt = DateTime.UtcNow,
                },
            },
        };

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync(ct);

        return await ReadWorkspaceDtoAsync(workspace.Id, ct);
    }

    public async Task<WorkspaceResponseDto> UpdateWorkspaceAsync(
        Guid id,
        UpdateWorkspaceRequest dto,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace =
            await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        // Clients render a personal workspace from a translation key rather than this column,
        // because "X's Workspace" is English *grammar* — Romanian needs "Spațiul de lucru al
        // lui X", a different construction entirely, so a stored name cannot be translated.
        // Allowing a rename that nothing ever displays would be worse than refusing it.
        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNotRenamable,
                "Your personal workspace cannot be renamed."
            );

        workspace.Name = dto.Name;
        workspace.Description = dto.Description;

        await _context.SaveChangesAsync(ct);

        return await ReadWorkspaceDtoAsync(id, ct);
    }

    public async Task DeleteWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace =
            await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNotDeletable,
                "Your personal workspace cannot be deleted."
            );

        // IgnoreQueryFilters: a trashed project still pins the workspace, or restoring it
        // later drops it into one nobody can reach.
        bool hasProjects = await _context
            .Projects.IgnoreQueryFilters()
            .AnyAsync(p => p.WorkspaceId == id, ct);

        if (hasProjects)
            throw new BusinessRuleException(
                BusinessRuleCodes.WorkspaceHasProjects,
                "This workspace still holds projects."
            );

        // Not Remove(): WorkspaceMember cascades from Workspace, so removing the principal
        // would hard-delete the membership rows this workspace needs to come back.
        workspace.IsDeleted = true;
        workspace.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task<WorkspaceResponseDto> RestoreWorkspaceAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace =
            await _context.Workspaces.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsDeleted)
        {
            workspace.IsDeleted = false;
            workspace.DeletedAt = null;
            await _context.SaveChangesAsync(ct);
        }

        return await ReadWorkspaceDtoAsync(id, ct);
    }

    public async Task<IEnumerable<WorkspaceResponseDto>> GetDeletedWorkspacesAsync(
        CancellationToken ct = default
    )
    {
        return await _context
            .Workspaces.IgnoreQueryFilters()
            .Where(w =>
                w.IsDeleted
                && w.Members.Any(m =>
                    m.UserId == _currentUser.UserGuid && m.Role == WorkspaceRole.Owner
                )
            )
            .OrderByDescending(w => w.DeletedAt)
            .MapToDto(_currentUser.UserGuid)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<WorkspaceMemberResponseDto>> GetMembersAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireMemberAsync(id, ct);

        return await _context
            .WorkspaceMembers.Where(m => m.WorkspaceId == id)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<WorkspaceMemberResponseDto> AddMemberAsync(
        Guid id,
        AddMemberRequest dto,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace =
            await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNoMembers,
                "A personal workspace cannot have other members."
            );

        var userExists = await _context.Users.AnyAsync(
            u => u.Id == dto.UserId && !u.IsAnonymized,
            ct
        );

        if (!userExists)
            throw new NotFoundException("User not found.");

        if (await _context.IsAdminAsync(dto.UserId, ct))
            throw new BusinessRuleException(
                BusinessRuleCodes.AdminCannotJoinWorkspace,
                "Administrator accounts cannot join workspaces."
            );

        bool alreadyMember = await _context.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == id && m.UserId == dto.UserId,
            ct
        );

        if (alreadyMember)
            throw new BusinessRuleException(
                BusinessRuleCodes.AlreadyWorkspaceMember,
                "This user is already a member of the workspace."
            );

        var member = new WorkspaceMember
        {
            WorkspaceId = id,
            UserId = dto.UserId,
            Role = dto.Role ?? WorkspaceRole.Member,
            JoinedAt = DateTime.UtcNow,
        };

        _context.WorkspaceMembers.Add(member);
        await _context.SaveChangesAsync(ct);

        return await ReadMemberDtoAsync(id, dto.UserId, ct);
    }

    public async Task<WorkspaceMemberResponseDto> ChangeRoleAsync(
        Guid id,
        Guid userId,
        WorkspaceRole newRole,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var member = await FindMemberAsync(id, userId, ct);

        if (member.Role == WorkspaceRole.Owner && newRole != WorkspaceRole.Owner)
            await RequireNotLastOwnerAsync(id, userId, ct);

        member.Role = newRole;
        await _context.SaveChangesAsync(ct);

        return await ReadMemberDtoAsync(id, userId, ct);
    }

    public async Task RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var member = await FindMemberAsync(id, userId, ct);

        if (member.Role == WorkspaceRole.Owner)
            await RequireNotLastOwnerAsync(id, userId, ct);

        _context.WorkspaceMembers.Remove(member);
        await ClearTaskAssignmentsAsync(id, userId, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task LeaveAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _accessService.RequireMemberAsync(id, ct);
        var userId = RequireCurrentUserId();

        var workspace =
            await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNotLeavable,
                "You cannot leave your personal workspace."
            );

        if (role == WorkspaceRole.Owner)
            await RequireNotLastOwnerAsync(id, userId, ct);

        var member = await FindMemberAsync(id, userId, ct);

        _context.WorkspaceMembers.Remove(member);
        await ClearTaskAssignmentsAsync(id, userId, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task EnsurePersonalWorkspaceAsync(User user, CancellationToken ct = default)
    {
        bool exists = await _context.Workspaces.AnyAsync(
            w => w.IsPersonal && w.Members.Any(m => m.UserId == user.Id),
            ct
        );

        if (exists)
            return;

        _context.Workspaces.Add(
            new Workspace
            {
                Name = PersonalWorkspaceName(user),
                IsPersonal = true,
                CreatedBy = user.Id,
                Members =
                {
                    new WorkspaceMember
                    {
                        UserId = user.Id,
                        Role = WorkspaceRole.Owner,
                        JoinedAt = DateTime.UtcNow,
                    },
                },
            }
        );

        await _context.SaveChangesAsync(ct);
    }

    private const string PersonalWorkspaceSuffix = "'s Workspace";

    /// <summary>
    /// Truncates the owner segment so the derived name fits the column. FirstName is validated
    /// at 50 and the suffix costs 12, so an untruncated name reaches 62 against a 60-char column
    /// and registration fails with Postgres 22001. Nothing the caller submitted was too long —
    /// the overflow is in the derived value, which request validation cannot see.
    /// </summary>
    private static string PersonalWorkspaceName(User user)
    {
        var owner = string.IsNullOrWhiteSpace(user.Nickname) ? user.FirstName : user.Nickname;
        var maxOwner = Workspace.NameMaxLength - PersonalWorkspaceSuffix.Length;

        if (owner.Length > maxOwner)
            owner = owner[..maxOwner];

        return owner + PersonalWorkspaceSuffix;
    }

    /// <summary>
    /// Unassigns the departing user from every task in the workspace, reaching into
    /// soft-deleted projects too: restoring one would otherwise bring back a card naming
    /// someone the remaining members can no longer see.
    /// </summary>
    private async Task ClearTaskAssignmentsAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken ct
    )
    {
        var assigned = await _context
            .Tasks.IgnoreQueryFilters()
            .InProjectsOf(
                _context.Projects.IgnoreQueryFilters().Where(p => p.WorkspaceId == workspaceId)
            )
            .Where(t => t.AssigneeId == userId)
            .ToListAsync(ct);

        foreach (var task in assigned)
            task.AssigneeId = null;
    }

    private Guid RequireCurrentUserId() =>
        _currentUser.UserGuid ?? throw new UnauthorizedAccessException("No authenticated user.");

    private async Task<WorkspaceMember> FindMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken ct
    ) =>
        await _context.WorkspaceMembers.FirstOrDefaultAsync(
            m => m.WorkspaceId == workspaceId && m.UserId == userId,
            ct
        ) ?? throw new NotFoundException("This user is not a member of the workspace.");

    private async Task RequireNotLastOwnerAsync(Guid workspaceId, Guid userId, CancellationToken ct)
    {
        bool anotherOwnerExists = await _context.WorkspaceMembers.AnyAsync(
            m =>
                m.WorkspaceId == workspaceId && m.UserId != userId && m.Role == WorkspaceRole.Owner,
            ct
        );

        if (!anotherOwnerExists)
            throw new BusinessRuleException(
                BusinessRuleCodes.WorkspaceMustHaveOwner,
                "A workspace must have at least one owner."
            );
    }

    private async Task<WorkspaceResponseDto> ReadWorkspaceDtoAsync(Guid id, CancellationToken ct) =>
        await _context
            .Workspaces.IgnoreQueryFilters()
            .Where(w => w.Id == id)
            .MapToDto(_currentUser.UserGuid)
            .FirstAsync(ct);

    private async Task<WorkspaceMemberResponseDto> ReadMemberDtoAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken ct
    ) =>
        await _context
            .WorkspaceMembers.Where(m => m.WorkspaceId == workspaceId && m.UserId == userId)
            .MapToDto()
            .FirstAsync(ct);
}
