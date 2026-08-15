using Backend.Config;
using Backend.Data;
using Backend.DTOs.Workspace;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Admin;

public class AdminWorkspaceService : AdminTrashService<Workspace>, IAdminWorkspaceService
{
    public AdminWorkspaceService(AppDbContext context)
        : base(context) { }

    public async Task<IEnumerable<AdminWorkspaceResponseDto>> GetAllDeletedWorkspacesAsync(
        CancellationToken ct = default
    )
    {
        return await Context
            .Workspaces.IgnoreQueryFilters()
            .Include(w => w.Creator)
            .Include(w => w.Updater)
            .Where(w => w.IsDeleted)
            .OrderByDescending(w => w.DeletedAt)
            .MapToAdminDto()
            .ToListAsync(ct);
    }

    public async Task<int> RestoreWorkspacesAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var workspaces = await Context
            .Workspaces.IgnoreQueryFilters()
            .Where(w => ids.Contains(w.Id) && w.IsDeleted)
            .ToListAsync(ct);

        foreach (var workspace in workspaces)
        {
            workspace.IsDeleted = false;
            workspace.DeletedAt = null;
        }

        await Context.SaveChangesAsync(ct);

        return workspaces.Count;
    }

    public async Task<int> PurgeWorkspacesAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var workspaces = await Context
            .Workspaces.IgnoreQueryFilters()
            .Where(w => ids.Contains(w.Id) && w.IsDeleted)
            .ToListAsync(ct);

        if (workspaces.Count == 0)
            return 0;

        var purgeIds = workspaces.ConvertAll(w => w.Id);

        // Unreachable while DeleteWorkspaceAsync refuses a workspace holding projects.
        // Kept because the FK is Restrict: an anomaly would otherwise surface as a 500.
        bool holdsProjects = await Context
            .Projects.IgnoreQueryFilters()
            .AnyAsync(p => purgeIds.Contains(p.WorkspaceId), ct);

        if (holdsProjects)
            throw new BusinessRuleException(
                BusinessRuleCodes.WorkspaceHasProjects,
                "A workspace that still holds projects cannot be purged."
            );

        foreach (var workspace in workspaces)
        {
            // Members and invitations cascade in the database; neither is an audit
            // entity, so the soft-delete interception does not rescue them.
            Context.MarkForHardDelete(workspace);
            Context.Workspaces.Remove(workspace);
        }

        await Context.SaveChangesAsync(ct);

        return workspaces.Count;
    }
}
