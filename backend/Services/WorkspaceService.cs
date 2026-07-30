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

    public WorkspaceService(AppDbContext context, ICurrentUserService currentUser, IWorkspaceAccessService accessService)
        : base(context, currentUser)
    {
        _accessService = accessService;
    }

    public async Task<IEnumerable<WorkspaceResponseDto>> GetMyWorkspacesAsync(CancellationToken ct = default)
    {
        return await _context.Workspaces
            .Where(w => w.Members.Any(m => m.UserId == _currentUser.UserGuid))
            .OrderByDescending(w => w.IsPersonal)
            .ThenBy(w => w.Name)
            .MapToDto(_currentUser.UserGuid)
            .ToListAsync(ct);
    }

    public async Task<WorkspaceResponseDto?> GetWorkspaceByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Workspaces
            .Where(w => w.Id == id && w.Members.Any(m => m.UserId == _currentUser.UserGuid))
            .MapToDto(_currentUser.UserGuid)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<WorkspaceResponseDto> CreateWorkspaceAsync(CreateWorkspaceRequest dto, CancellationToken ct = default)
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
                    JoinedAt = DateTime.UtcNow
                }
            }
        };

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync(ct);

        return await ReadWorkspaceDtoAsync(workspace.Id, ct);
    }

    public async Task<WorkspaceResponseDto> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceRequest dto, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace = await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        workspace.Name = dto.Name;
        workspace.Description = dto.Description;

        await _context.SaveChangesAsync(ct);

        return await ReadWorkspaceDtoAsync(id, ct);
    }

    public async Task DeleteWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace = await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(BusinessRuleCodes.PersonalWorkspaceNotDeletable,
                "Your personal workspace cannot be deleted.");

        // TODO(stage 2): also refuse when the workspace holds any project, active or trashed.
        // Not expressible until Project.WorkspaceId exists.

        _context.Workspaces.Remove(workspace);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<WorkspaceResponseDto> RestoreWorkspaceAsync(Guid id, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        _ = await RestoreAnyByIdAsync(id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        return await ReadWorkspaceDtoAsync(id, ct);
    }

    public async Task<IEnumerable<WorkspaceResponseDto>> GetDeletedWorkspacesAsync(CancellationToken ct = default)
    {
        return await _context.Workspaces
            .IgnoreQueryFilters()
            .Where(w => w.IsDeleted && w.Members.Any(m =>
                m.UserId == _currentUser.UserGuid && m.Role == WorkspaceRole.Owner))
            .OrderByDescending(w => w.DeletedAt)
            .MapToDto(_currentUser.UserGuid)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<WorkspaceMemberResponseDto>> GetMembersAsync(Guid id, CancellationToken ct = default)
    {
        await _accessService.RequireMemberAsync(id, ct);

        return await _context.WorkspaceMembers
            .Where(m => m.WorkspaceId == id)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<WorkspaceMemberResponseDto> AddMemberAsync(Guid id, AddMemberRequest dto, CancellationToken ct = default)
    {
        await _accessService.RequireOwnerAsync(id, ct);

        var workspace = await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(BusinessRuleCodes.PersonalWorkspaceNoMembers,
                "A personal workspace cannot have other members.");

        var userExists = await _context.Users
            .AnyAsync(u => u.Id == dto.UserId && !u.IsAnonymized, ct);

        if (!userExists)
            throw new NotFoundException("User not found.");

        bool alreadyMember = await _context.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == id && m.UserId == dto.UserId, ct);

        if (alreadyMember)
            throw new BusinessRuleException(BusinessRuleCodes.AlreadyWorkspaceMember,
                "This user is already a member of the workspace.");

        var member = new WorkspaceMember
        {
            WorkspaceId = id,
            UserId = dto.UserId,
            Role = dto.Role,
            JoinedAt = DateTime.UtcNow
        };

        _context.WorkspaceMembers.Add(member);
        await _context.SaveChangesAsync(ct);

        return await ReadMemberDtoAsync(id, dto.UserId, ct);
    }

    public async Task<WorkspaceMemberResponseDto> ChangeRoleAsync(Guid id, Guid userId, WorkspaceRole newRole, CancellationToken ct = default)
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
        await _context.SaveChangesAsync(ct);
    }

    public async Task LeaveAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _accessService.RequireMemberAsync(id, ct);
        var userId = RequireCurrentUserId();

        var workspace = await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");

        if (workspace.IsPersonal)
            throw new BusinessRuleException(BusinessRuleCodes.PersonalWorkspaceNotLeavable,
                "You cannot leave your personal workspace.");

        if (role == WorkspaceRole.Owner)
            await RequireNotLastOwnerAsync(id, userId, ct);

        var member = await FindMemberAsync(id, userId, ct);

        _context.WorkspaceMembers.Remove(member);
        await _context.SaveChangesAsync(ct);
    }

    public async Task EnsurePersonalWorkspaceAsync(User user, CancellationToken ct = default)
    {
        bool exists = await _context.Workspaces
            .AnyAsync(w => w.IsPersonal && w.Members.Any(m => m.UserId == user.Id), ct);

        if (exists) return;

        var owner = string.IsNullOrWhiteSpace(user.Nickname) ? user.FirstName : user.Nickname;

        _context.Workspaces.Add(new Workspace
        {
            Name = $"{owner}'s Workspace",
            IsPersonal = true,
            CreatedBy = user.Id,
            Members =
            {
                new WorkspaceMember
                {
                    UserId = user.Id,
                    Role = WorkspaceRole.Owner,
                    JoinedAt = DateTime.UtcNow
                }
            }
        });

        await _context.SaveChangesAsync(ct);
    }

    private Guid RequireCurrentUserId() =>
        _currentUser.UserGuid ?? throw new UnauthorizedAccessException("No authenticated user.");

    private async Task<WorkspaceMember> FindMemberAsync(Guid workspaceId, Guid userId, CancellationToken ct) =>
        await _context.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, ct)
        ?? throw new NotFoundException("This user is not a member of the workspace.");

    private async Task RequireNotLastOwnerAsync(Guid workspaceId, Guid userId, CancellationToken ct)
    {
        bool anotherOwnerExists = await _context.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId
                        && m.UserId != userId
                        && m.Role == WorkspaceRole.Owner, ct);

        if (!anotherOwnerExists)
            throw new BusinessRuleException(BusinessRuleCodes.WorkspaceMustHaveOwner,
                "A workspace must have at least one owner.");
    }

    private async Task<WorkspaceResponseDto> ReadWorkspaceDtoAsync(Guid id, CancellationToken ct) =>
        await _context.Workspaces
            .IgnoreQueryFilters()
            .Where(w => w.Id == id)
            .MapToDto(_currentUser.UserGuid)
            .FirstAsync(ct);

    private async Task<WorkspaceMemberResponseDto> ReadMemberDtoAsync(Guid workspaceId, Guid userId, CancellationToken ct) =>
        await _context.WorkspaceMembers
            .Where(m => m.WorkspaceId == workspaceId && m.UserId == userId)
            .MapToDto()
            .FirstAsync(ct);
}
