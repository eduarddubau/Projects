using Backend.Config;
using Backend.Data;
using Backend.DTOs.Workspace;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class WorkspaceService : BaseService<Workspace>, IWorkspaceService
{
    private readonly IWorkspaceAccessService _accessService;

    public WorkspaceService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAccessService accessService
    )
        : base(context, currentUser)
    {
        _accessService = accessService;
    }

    public async Task<IEnumerable<WorkspaceResponseDto>> GetMyWorkspacesAsync(
        CancellationToken ct = default
    )
    {
        return await Context
            .Workspaces.Where(w => w.Members.Any(m => m.UserId == CurrentUser.UserGuid))
            .OrderByDescending(w => w.IsPersonal)
            .ThenBy(w => w.Name)
            .MapToDto(CurrentUser.UserGuid)
            .ToListAsync(ct);
    }

    public async Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        return await Context
            .Workspaces.Where(w =>
                w.Id == id && w.Members.Any(m => m.UserId == CurrentUser.UserGuid)
            )
            .MapToDto(CurrentUser.UserGuid)
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

        Context.Workspaces.Add(workspace);
        await Context.SaveChangesAsync(ct);

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
            await Context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
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

        await Context.SaveChangesAsync(ct);

        return await ReadWorkspaceDtoAsync(id, ct);
    }

    public async Task DeleteWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace =
            await Context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNotDeletable,
                "Your personal workspace cannot be deleted."
            );

        // IgnoreQueryFilters: a trashed project still pins the workspace, or restoring it
        // later drops it into one nobody can reach.
        bool hasProjects = await Context
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

        await Context.SaveChangesAsync(ct);
    }

    public async Task<WorkspaceResponseDto> RestoreWorkspaceAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireOwnerAsync(id, ct);

        _ =
            await RestoreAnyByIdAsync(id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        return await ReadWorkspaceDtoAsync(id, ct);
    }

    public async Task<IEnumerable<WorkspaceResponseDto>> GetDeletedWorkspacesAsync(
        CancellationToken ct = default
    )
    {
        return await Context
            .Workspaces.IgnoreQueryFilters()
            .Where(w =>
                w.IsDeleted
                && w.Members.Any(m =>
                    m.UserId == CurrentUser.UserGuid && m.Role == WorkspaceRole.Owner
                )
            )
            .OrderByDescending(w => w.DeletedAt)
            .MapToDto(CurrentUser.UserGuid)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<WorkspaceMemberResponseDto>> GetMembersAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        await _accessService.RequireMemberAsync(id, ct);

        return await Context
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
            await Context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNoMembers,
                "A personal workspace cannot have other members."
            );

        var userExists = await Context.Users.AnyAsync(
            u => u.Id == dto.UserId && !u.IsAnonymized,
            ct
        );

        if (!userExists)
            throw new NotFoundException("User not found.");

        bool alreadyMember = await Context.WorkspaceMembers.AnyAsync(
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

        Context.WorkspaceMembers.Add(member);
        await Context.SaveChangesAsync(ct);

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
        await Context.SaveChangesAsync(ct);

        return await ReadMemberDtoAsync(id, userId, ct);
    }

    public async Task RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var member = await FindMemberAsync(id, userId, ct);

        if (member.Role == WorkspaceRole.Owner)
            await RequireNotLastOwnerAsync(id, userId, ct);

        Context.WorkspaceMembers.Remove(member);
        await Context.SaveChangesAsync(ct);
    }

    public async Task LeaveAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _accessService.RequireMemberAsync(id, ct);
        var userId = RequireCurrentUserId();

        var workspace =
            await Context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(
                BusinessRuleCodes.PersonalWorkspaceNotLeavable,
                "You cannot leave your personal workspace."
            );

        if (role == WorkspaceRole.Owner)
            await RequireNotLastOwnerAsync(id, userId, ct);

        var member = await FindMemberAsync(id, userId, ct);

        Context.WorkspaceMembers.Remove(member);
        await Context.SaveChangesAsync(ct);
    }

    public async Task EnsurePersonalWorkspaceAsync(User user, CancellationToken ct = default)
    {
        bool exists = await Context.Workspaces.AnyAsync(
            w => w.IsPersonal && w.Members.Any(m => m.UserId == user.Id),
            ct
        );

        if (exists)
            return;

        Context.Workspaces.Add(
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

        await Context.SaveChangesAsync(ct);
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

    private Guid RequireCurrentUserId() =>
        CurrentUser.UserGuid ?? throw new UnauthorizedAccessException("No authenticated user.");

    private async Task<WorkspaceMember> FindMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken ct
    ) =>
        await Context.WorkspaceMembers.FirstOrDefaultAsync(
            m => m.WorkspaceId == workspaceId && m.UserId == userId,
            ct
        ) ?? throw new NotFoundException("This user is not a member of the workspace.");

    private async Task RequireNotLastOwnerAsync(Guid workspaceId, Guid userId, CancellationToken ct)
    {
        bool anotherOwnerExists = await Context.WorkspaceMembers.AnyAsync(
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
        await Context
            .Workspaces.IgnoreQueryFilters()
            .Where(w => w.Id == id)
            .MapToDto(CurrentUser.UserGuid)
            .FirstAsync(ct);

    private async Task<WorkspaceMemberResponseDto> ReadMemberDtoAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken ct
    ) =>
        await Context
            .WorkspaceMembers.Where(m => m.WorkspaceId == workspaceId && m.UserId == userId)
            .MapToDto()
            .FirstAsync(ct);
}
