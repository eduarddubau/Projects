using Backend.DTOs.Workspace;
using Backend.Models;

namespace Backend.Mappings;

public static class WorkspaceMappingExtensions
{
    public static IQueryable<WorkspaceResponseDto> MapToDto(
        this IQueryable<Workspace> query,
        Guid? userId
    )
    {
        return query.Select(w => new WorkspaceResponseDto
        {
            Id = w.Id,
            Name = w.Name,
            Description = w.Description,
            IsPersonal = w.IsPersonal,
            MyRole = w.Members.Where(m => m.UserId == userId).Select(m => m.Role).FirstOrDefault(),
            MemberCount = w.Members.Count,
            // Explicit predicate, not w.Projects.Count: GetDeletedWorkspacesAsync runs
            // under IgnoreQueryFilters, where the soft-delete filter would not apply.
            ProjectCount = w.Projects.Count(p => !p.IsDeleted),
            IsDeleted = w.IsDeleted,
            DeletedAt = w.DeletedAt,
            CreatedAt = w.CreatedAt,
            CreatedBy = w.CreatedBy,
            CreatedByDisplayName =
                w.Creator == null ? string.Empty : w.Creator.FirstName + " " + w.Creator.LastName,
            UpdatedAt = w.UpdatedAt,
            UpdatedBy = w.UpdatedBy,
            UpdatedByDisplayName =
                w.Updater == null ? string.Empty : w.Updater.FirstName + " " + w.Updater.LastName,
        });
    }

    public static IQueryable<WorkspaceMemberResponseDto> MapToDto(
        this IQueryable<WorkspaceMember> query
    )
    {
        return query.Select(m => new WorkspaceMemberResponseDto
        {
            WorkspaceId = m.WorkspaceId,
            UserId = m.UserId,
            UserDisplayName =
                m.User == null ? string.Empty : m.User.FirstName + " " + m.User.LastName,
            Role = m.Role,
            JoinedAt = m.JoinedAt,
        });
    }

    public static IQueryable<InvitationResponseDto> MapToDto(this IQueryable<Invitation> query)
    {
        return query.Select(i => new InvitationResponseDto
        {
            Id = i.Id,
            WorkspaceId = i.WorkspaceId,
            Email = i.Email,
            Role = i.Role,
            CreatedAt = i.CreatedAt,
            ExpiresAt = i.ExpiresAt,
            InvitedByDisplayName =
                i.Inviter == null ? string.Empty : i.Inviter.FirstName + " " + i.Inviter.LastName,
        });
    }

    public static WorkspaceMemberResponseDto MapToDto(this WorkspaceMember member) =>
        new()
        {
            WorkspaceId = member.WorkspaceId,
            UserId = member.UserId,
            UserDisplayName = member.User.GetDisplayName() ?? string.Empty,
            Role = member.Role,
            JoinedAt = member.JoinedAt,
        };
}
