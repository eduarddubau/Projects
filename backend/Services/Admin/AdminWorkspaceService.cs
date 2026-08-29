using Backend.Config;
using Backend.Data;
using Backend.DTOs.Workspace;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Admin.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services.Admin;

public class AdminWorkspaceService : IAdminWorkspaceService
{
    private readonly AppDbContext _context;

    public AdminWorkspaceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AdminWorkspaceResponseDto>> GetAllDeletedWorkspacesAsync(
        CancellationToken ct = default
    )
    {
        return await _context
            .Workspaces.IgnoreQueryFilters()
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
        var workspaces = await _context
            .Workspaces.IgnoreQueryFilters()
            .Where(w => ids.Contains(w.Id) && w.IsDeleted)
            .ToListAsync(ct);

        foreach (var workspace in workspaces)
        {
            workspace.IsDeleted = false;
            workspace.DeletedAt = null;
        }

        await _context.SaveChangesAsync(ct);

        return workspaces.Count;
    }

    public async Task<int> PurgeWorkspacesAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var workspaces = await _context
            .Workspaces.IgnoreQueryFilters()
            .Where(w => ids.Contains(w.Id) && w.IsDeleted)
            .ToListAsync(ct);

        if (workspaces.Count == 0)
            return 0;

        var purgeIds = workspaces.ConvertAll(w => w.Id);

        // Unreachable while DeleteWorkspaceAsync refuses a workspace holding projects.
        // Kept because the FK is Restrict: an anomaly would otherwise surface as a 500.
        bool holdsProjects = await _context
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
            _context.MarkForHardDelete(workspace);
            _context.Workspaces.Remove(workspace);
        }

        await _context.SaveChangesAsync(ct);

        return workspaces.Count;
    }
}
