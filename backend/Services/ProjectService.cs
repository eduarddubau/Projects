using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

/// <summary>Projects the caller can reach through workspace membership.</summary>
public class ProjectService : IProjectService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceAccessService _access;
    private readonly int _trashWindowDays;

    public ProjectService(
        AppDbContext context,
        ICurrentUserService currentUser,
        IWorkspaceAccessService access,
        IOptions<ProjectRetentionOptions> retentionOptions
    )
    {
        _context = context;
        _currentUser = currentUser;
        _access = access;
        _trashWindowDays = retentionOptions.Value.TrashWindowDays;
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
            .Projects.Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.WorkspaceId == workspaceId)
            .OrderByDescending(p => p.CreatedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetWorkspaceDeletedProjectsAsync(
        Guid workspaceId,
        CancellationToken ct = default
    )
    {
        await _access.RequireMemberAsync(workspaceId, ct);

        var cutoff = DateTime.UtcNow.AddDays(-_trashWindowDays);

        return await _context
            .Projects.IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .Where(p => p.WorkspaceId == workspaceId && p.IsDeleted && p.DeletedAt >= cutoff)
            .OrderByDescending(p => p.DeletedAt)
            .MapToDto()
            .ToListAsync(ct);
    }

    public async Task<ProjectResponseDto?> GetProjectByIdAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var project = await AccessibleProjects
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
            project.IsDeleted = false;
            project.DeletedAt = null;
            await _context.SaveChangesAsync(ct);
        }

        return await LoadDtoAsync(project, ct);
    }

    public async Task<ProjectResponseDto?> MoveProjectAsync(
        Guid id,
        Guid targetWorkspaceId,
        CancellationToken ct = default
    )
    {
        var project = await AccessibleProjects.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return null;

        if (project.WorkspaceId == targetWorkspaceId)
            return await LoadDtoAsync(project, ct);

        await _access.RequireOwnerAsync(project.WorkspaceId, ct);
        await _access.RequireMemberAsync(targetWorkspaceId, ct);

        await RequireNameIsFreeAsync(targetWorkspaceId, project.Name, id, ct);

        project.WorkspaceId = targetWorkspaceId;
        await _context.SaveChangesAsync(ct);

        return await LoadDtoAsync(project, ct);
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
