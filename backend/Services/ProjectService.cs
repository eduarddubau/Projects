using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Projects the caller can reach through workspace membership.</summary>
public class ProjectService : IProjectService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAccessService _access;
    private readonly TrashWindow _trashWindow;

    public ProjectService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAccessService access,
        TrashWindow trashWindow
    )
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _trashWindow = trashWindow;
    }

    // A non-member gets null, which the controller turns into 404.
    private IQueryable<Project> AccessibleProjects =>
        _context.Projects.InWorkspacesOf(_context.WorkspaceMembers, _currentUser.UserGuid);

    public async Task<IEnumerable<ProjectResponseDto>> GetWorkspaceProjectsAsync(
        Guid workspaceId,
        CancellationToken ct = default
    )
    {
        await _access.RequireMemberAsync(workspaceId, ct);

        return await _context
            .Projects.Where(p => p.WorkspaceId == workspaceId)
            .OrderByDescending(p => p.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    // Owner, not member: this list exists to serve restore, and restore is owner-only.
    public async Task<IEnumerable<ProjectResponseDto>> GetWorkspaceDeletedProjectsAsync(
        Guid workspaceId,
        CancellationToken ct = default
    )
    {
        await _access.RequireOwnerAsync(workspaceId, ct);

        var cutoff = _trashWindow.Cutoff;

        return await _context
            .Projects.IgnoreQueryFilters()
            .Where(p => p.WorkspaceId == workspaceId && p.IsDeleted && p.DeletedAt >= cutoff)
            .OrderByDescending(p => p.DeletedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    // IgnoreQueryFilters so a trashed project still has a page — restoring one happens there.
    // Safe to compose here, unlike in TaskService.RestoreTaskByIdAsync: the only filter it drops
    // is the Projects one, since InWorkspacesOf reads workspace_members, which has none.
    public async Task<ProjectResponseDto?> GetProjectByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var project = await _context
            .Projects.IgnoreQueryFilters()
            .InWorkspacesOf(_context.WorkspaceMembers, _currentUser.UserGuid)
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return project?.MapToDto();
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(
        Guid workspaceId,
        CreateProjectRequest dto,
        CancellationToken ct = default
    )
    {
        await _access.RequireMemberAsync(workspaceId, ct);
        await RequireNameIsFreeAsync(workspaceId, dto.Name, null, ct);

        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
            WorkspaceId = workspaceId,
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);

        return await LoadDtoAsync(project, ct);
    }

    public async Task<ProjectResponseDto?> UpdateProjectAsync(
        Guid id,
        UpdateProjectRequest dto,
        CancellationToken ct = default
    )
    {
        var project = await AccessibleProjects.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return null;

        await RequireNameIsFreeAsync(project.WorkspaceId, dto.Name, id, ct);

        project.Name = dto.Name;
        project.Description = dto.Description;

        await _context.SaveChangesAsync(ct);

        return await LoadDtoAsync(project, ct);
    }

    public async Task<bool> DeleteProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await AccessibleProjects.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return false;

        await _access.RequireOwnerAsync(project.WorkspaceId, ct);

        project.IsDeleted = true;
        project.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<ProjectResponseDto?> RestoreProjectByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var project = await _context
            .Projects.IgnoreQueryFilters()
            .InWorkspacesOf(_context.WorkspaceMembers, _currentUser.UserGuid)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return null;

        await _access.RequireOwnerAsync(project.WorkspaceId, ct);

        // Looks dead — DeleteWorkspaceAsync refuses a workspace holding projects — but an
        // admin purge can empty one and unblock its deletion.
        bool workspaceIsDeleted = await _context
            .Workspaces.IgnoreQueryFilters()
            .AnyAsync(w => w.Id == project.WorkspaceId && w.IsDeleted, ct);

        if (workspaceIsDeleted)
            throw new BusinessRuleException(
                BusinessRuleCodes.WorkspaceIsDeleted,
                "This project's workspace has been deleted."
            );

        if (project.IsDeleted)
        {
            // The window is policy, not a display filter: a project the trash stopped listing
            // must not stay restorable by anyone who kept its id. The admin trash restores
            // past this, deliberately — it has no window at all.
            if (project.DeletedAt < _trashWindow.Cutoff)
                return null;

            project.IsDeleted = false;
            project.DeletedAt = null;
            await _context.SaveChangesAsync(ct);
        }

        return await LoadDtoAsync(project, ct);
    }

    public async Task<MoveProjectResponseDto?> MoveProjectAsync(
        Guid id,
        Guid targetWorkspaceId,
        CancellationToken ct = default
    )
    {
        var project = await AccessibleProjects.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return null;

        if (project.WorkspaceId == targetWorkspaceId)
            return new MoveProjectResponseDto(await LoadDtoAsync(project, ct), 0);

        await _access.RequireOwnerAsync(project.WorkspaceId, ct);
        await _access.RequireMemberAsync(targetWorkspaceId, ct);

        await RequireNameIsFreeAsync(targetWorkspaceId, project.Name, id, ct);

        project.WorkspaceId = targetWorkspaceId;
        var unassignedCount = await UnassignNonMembersAsync(id, targetWorkspaceId, ct);
        await _context.SaveChangesAsync(ct);

        return new MoveProjectResponseDto(await LoadDtoAsync(project, ct), unassignedCount);
    }

    /// <summary>
    /// Unassigns anyone the target workspace does not hold, and reports how many. Reaches
    /// past the soft-delete filter: a restored task would otherwise carry back a name its
    /// new workspace cannot resolve.
    /// </summary>
    private async Task<int> UnassignNonMembersAsync(
        Guid projectId,
        Guid targetWorkspaceId,
        CancellationToken ct
    )
    {
        var strangers = await _context
            .Tasks.IgnoreQueryFilters()
            .Where(t =>
                t.ProjectId == projectId
                && t.AssigneeId != null
                && !_context.WorkspaceMembers.Any(m =>
                    m.WorkspaceId == targetWorkspaceId && m.UserId == t.AssigneeId
                )
            )
            .ToListAsync(ct);

        foreach (var task in strangers)
            task.AssigneeId = null;

        return strangers.Count;
    }

    private async Task RequireNameIsFreeAsync(
        Guid workspaceId,
        string name,
        Guid? excludeId,
        CancellationToken ct
    )
    {
        var query = _context.Projects.Where(p => p.WorkspaceId == workspaceId && p.Name == name);

        if (excludeId is Guid self)
            query = query.Where(p => p.Id != self);

        if (await query.AnyAsync(ct))
            throw new BusinessRuleException(
                BusinessRuleCodes.DuplicateProjectName,
                "This workspace already has a project with this name."
            );
    }

    private async Task<ProjectResponseDto> LoadDtoAsync(Project project, CancellationToken ct)
    {
        await _context.Entry(project).Reference(p => p.Creator).LoadAsync(ct);
        await _context.Entry(project).Reference(p => p.Updater).LoadAsync(ct);
        await _context.Entry(project).Reference(p => p.Workspace).LoadAsync(ct);

        return project.MapToDto();
    }
}
