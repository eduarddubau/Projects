using Backend.Data;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

// Not a BaseService: a read-only guard has no business inheriting soft-delete and restore.
public class WorkspaceAccessService : IWorkspaceAccessService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public WorkspaceAccessService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<WorkspaceRole> RequireMemberAsync(
        Guid workspaceId,
        CancellationToken ct = default
    )
    {
        // The row, not a projection: FirstOrDefaultAsync over a projected enum
        // returns default(WorkspaceRole), so a non-member would read as a Member.
        var membership = await _context
            .WorkspaceMembers.AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.WorkspaceId == workspaceId && m.UserId == _currentUser.UserGuid,
                ct
            );

        return membership?.Role ?? throw new NotFoundException("Workspace not found.");
    }

    public async Task RequireOwnerAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var role = await RequireMemberAsync(workspaceId, ct);
        if (role != WorkspaceRole.Owner)
            throw new UnauthorizedAccessException("Only workspace owners can do this.");
    }
}
