using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services;

public sealed class ProjectServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly ProjectService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private const int TrashWindowDays = 30;

    // Owner of the personal and owned ones, plain member of the shared one, absent
    // from the foreign one.
    private readonly Workspace _personal;
    private readonly Workspace _owned;
    private readonly Workspace _shared;
    private readonly Workspace _foreign;

    public ProjectServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);
        _currentUser.Setup(c => c.UserGuid).Returns(_userId);

        _service = new ProjectService(
            _context,
            _currentUser.Object,
            new WorkspaceAccessService(_context, _currentUser.Object),
            new TrashWindow { Days = TrashWindowDays }
        );

        _personal = AddWorkspace("Personal", isPersonal: true, (_userId, WorkspaceRole.Owner));
        _owned = AddWorkspace(
            "Owned",
            isPersonal: false,
            (_userId, WorkspaceRole.Owner),
            (_otherUserId, WorkspaceRole.Member)
        );
        _shared = AddWorkspace(
            "Shared",
            isPersonal: false,
            (_otherUserId, WorkspaceRole.Owner),
            (_userId, WorkspaceRole.Member)
        );
        _foreign = AddWorkspace("Foreign", isPersonal: false, (_otherUserId, WorkspaceRole.Owner));
    }

    private Workspace AddWorkspace(
        string name,
        bool isPersonal,
        params (Guid UserId, WorkspaceRole Role)[] members
    )
    {
        var workspace = new Workspace { Name = name, IsPersonal = isPersonal };

        foreach (var (userId, role) in members)
            workspace.Members.Add(
                new WorkspaceMember
                {
                    UserId = userId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                }
            );

        _context.Workspaces.Add(workspace);
        _context.SaveChanges();
        return workspace;
    }

    private Project AddProject(
        string name,
        Workspace workspace,
        Guid? createdBy = null,
        bool isDeleted = false,
        DateTime? deletedAt = null
    )
    {
        var project = new Project
        {
            Name = name,
            CreatedBy = createdBy ?? _userId,
            WorkspaceId = workspace.Id,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
        };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    private TaskItem AddTask(Project project, Guid assigneeId, bool isDeleted = false)
    {
        var task = new TaskItem
        {
            Title = project.Name + " work",
            ProjectId = project.Id,
            AssigneeId = assigneeId,
            IsDeleted = isDeleted,
        };

        _context.Tasks.Add(task);
        _context.SaveChanges();
        return task;
    }

    private async Task<Guid?> ReadAssigneeAsync(Guid taskId) =>
        (await _context.Tasks.IgnoreQueryFilters().FirstAsync(t => t.Id == taskId, Ct)).AssigneeId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetWorkspaceProjectsAsync_ReturnsOnlyThatWorkspacesProjects()
    {
        AddProject("In personal", _personal);
        AddProject("In shared", _shared);

        var result = await _service.GetWorkspaceProjectsAsync(_personal.Id, Ct);

        Assert.Equal(["In personal"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetWorkspaceProjectsAsync_IncludesProjectsCreatedByOtherMembers()
    {
        AddProject("Theirs", _shared, createdBy: _otherUserId);

        var result = await _service.GetWorkspaceProjectsAsync(_shared.Id, Ct);

        Assert.Equal(["Theirs"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetWorkspaceProjectsAsync_WhenNotAMember_Throws404NotFound()
    {
        AddProject("Not mine to see", _foreign, createdBy: _otherUserId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetWorkspaceProjectsAsync(_foreign.Id, Ct)
        );
    }

    [Fact]
    public async Task GetWorkspaceProjectsAsync_ExcludesDeletedProjects()
    {
        AddProject("Active", _personal);
        AddProject("Deleted", _personal, isDeleted: true);

        var result = await _service.GetWorkspaceProjectsAsync(_personal.Id, Ct);

        Assert.Equal(["Active"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetWorkspaceDeletedProjectsAsync_ReturnsDeletedProjectsWithinTheTrashWindow()
    {
        AddProject("Active", _personal);
        AddProject(
            "RecentlyDeleted",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-1)
        );
        AddProject(
            "OldDeleted",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );

        var result = await _service.GetWorkspaceDeletedProjectsAsync(_personal.Id, Ct);

        Assert.Equal(["RecentlyDeleted"], result.Select(p => p.Name));
    }

    // The trash is the workspace's, not "things I deleted".
    [Fact]
    public async Task GetWorkspaceDeletedProjectsAsync_IncludesProjectsDeletedByOtherMembers()
    {
        AddProject(
            "Theirs",
            _owned,
            createdBy: _otherUserId,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-1)
        );

        var result = await _service.GetWorkspaceDeletedProjectsAsync(_owned.Id, Ct);

        Assert.Equal(["Theirs"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetWorkspaceDeletedProjectsAsync_WhenAMemberButNotTheOwner_Throws403()
    {
        AddProject("Theirs", _shared, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetWorkspaceDeletedProjectsAsync(_shared.Id, Ct)
        );
    }

    [Fact]
    public async Task GetWorkspaceDeletedProjectsAsync_WhenNotAMember_Throws404NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetWorkspaceDeletedProjectsAsync(_foreign.Id, Ct)
        );
    }

    [Fact]
    public async Task GetProjectByIdAsync_WhenInAWorkspaceIBelongTo_ReturnsDto()
    {
        var project = AddProject("Theirs", _shared, createdBy: _otherUserId);

        var result = await _service.GetProjectByIdAsync(project.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(project.Id, result!.Id);
        Assert.Equal(_shared.Id, result.WorkspaceId);
    }

    // A trashed project keeps its page: restoring happens there rather than from a button in
    // the trash table, so the detail route has to be able to load one.
    [Fact]
    public async Task GetProjectByIdAsync_WhenTheProjectIsInTheTrash_StillReturnsIt()
    {
        var project = AddProject(
            "Trashed",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-1)
        );

        var result = await _service.GetProjectByIdAsync(project.Id, Ct);

        Assert.NotNull(result);
        Assert.True(result.IsDeleted);
    }

    // The looser filter must not loosen the access check with it.
    [Fact]
    public async Task GetProjectByIdAsync_WhenTrashedInAWorkspaceIDoNotBelongTo_ReturnsNull()
    {
        var project = AddProject(
            "Theirs",
            _foreign,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-1)
        );

        Assert.Null(await _service.GetProjectByIdAsync(project.Id, Ct));
    }

    [Fact]
    public async Task GetProjectByIdAsync_WhenInAWorkspaceIDoNotBelongTo_ReturnsNull()
    {
        var project = AddProject("Foreign", _foreign, createdBy: _otherUserId);

        var result = await _service.GetProjectByIdAsync(project.Id, Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProjectAsync_AddsProjectToTheNamedWorkspace()
    {
        var request = new CreateProjectRequest("New Project", "A description");

        var result = await _service.CreateProjectAsync(_shared.Id, request, Ct);

        Assert.Equal("New Project", result.Name);
        Assert.Equal(_shared.Id, result.WorkspaceId);
        var stored = await _context.Projects.FirstAsync(p => p.Id == result.Id, Ct);
        Assert.Equal(_userId, stored.CreatedBy);
        Assert.Equal(_shared.Id, stored.WorkspaceId);
    }

    [Fact]
    public async Task CreateProjectAsync_WhenNotAMember_Throws404NotFound()
    {
        var request = new CreateProjectRequest("New Project", null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateProjectAsync(_foreign.Id, request, Ct)
        );
    }

    [Fact]
    public async Task CreateProjectAsync_WhenNameAlreadyExistsInThatWorkspace_Throws409Conflict()
    {
        AddProject("Duplicate", _personal);
        var request = new CreateProjectRequest("Duplicate", null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateProjectAsync(_personal.Id, request, Ct)
        );
    }

    [Fact]
    public async Task CreateProjectAsync_WhenNameExistsOnlyInAnotherWorkspace_Succeeds()
    {
        AddProject("Roadmap", _personal);
        var request = new CreateProjectRequest("Roadmap", null);

        var result = await _service.CreateProjectAsync(_shared.Id, request, Ct);

        Assert.Equal("Roadmap", result.Name);
        Assert.Equal(_shared.Id, result.WorkspaceId);
    }

    [Fact]
    public async Task CreateProjectAsync_WhenNameIsTakenByAnotherMember_Throws409Conflict()
    {
        AddProject("Roadmap", _shared, createdBy: _otherUserId);
        var request = new CreateProjectRequest("Roadmap", null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CreateProjectAsync(_shared.Id, request, Ct)
        );
    }

    [Fact]
    public async Task UpdateProjectAsync_WhenAPlainMemberOfTheWorkspace_Updates()
    {
        var project = AddProject("Old Name", _shared, createdBy: _otherUserId);
        var request = new UpdateProjectRequest("New Name", "New description");

        var result = await _service.UpdateProjectAsync(project.Id, request, Ct);

        Assert.NotNull(result);
        Assert.Equal("New Name", result!.Name);
        Assert.Equal("New description", result.Description);
    }

    [Fact]
    public async Task UpdateProjectAsync_WhenInAWorkspaceIDoNotBelongTo_ReturnsNull()
    {
        var project = AddProject("Old Name", _foreign, createdBy: _otherUserId);
        var request = new UpdateProjectRequest("New Name", null);

        var result = await _service.UpdateProjectAsync(project.Id, request, Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateProjectAsync_WhenNameConflictsInsideTheSameWorkspace_Throws409Conflict()
    {
        AddProject("Taken", _personal);
        var project = AddProject("Original", _personal);
        var request = new UpdateProjectRequest("Taken", null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateProjectAsync(project.Id, request, Ct)
        );
    }

    [Fact]
    public async Task UpdateProjectAsync_WhenRenamedToItsOwnName_Succeeds()
    {
        var project = AddProject("Same", _personal);
        var request = new UpdateProjectRequest("Same", "Changed description");

        var result = await _service.UpdateProjectAsync(project.Id, request, Ct);

        Assert.NotNull(result);
        Assert.Equal("Changed description", result!.Description);
    }

    [Fact]
    public async Task DeleteProjectByIdAsync_WhenOwnerOfTheWorkspace_SoftDeletesAndReturnsTrue()
    {
        var project = AddProject("Mine", _personal);

        var result = await _service.DeleteProjectByIdAsync(project.Id, Ct);

        Assert.True(result);
        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == project.Id, Ct);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteProjectByIdAsync_WhenAPlainMemberOfTheWorkspace_Throws403Forbidden()
    {
        var project = AddProject("Shared", _shared, createdBy: _otherUserId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteProjectByIdAsync(project.Id, Ct)
        );
    }

    [Fact]
    public async Task DeleteProjectByIdAsync_WhenAMemberDeletesTheirOwnProjectTheyDoNotOwn_Throws403Forbidden()
    {
        var project = AddProject("Mine but shared", _shared, createdBy: _userId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteProjectByIdAsync(project.Id, Ct)
        );
    }

    [Fact]
    public async Task DeleteProjectByIdAsync_WhenInAWorkspaceIDoNotBelongTo_ReturnsFalse()
    {
        var project = AddProject("Foreign", _foreign, createdBy: _otherUserId);

        var result = await _service.DeleteProjectByIdAsync(project.Id, Ct);

        Assert.False(result);
    }

    [Fact]
    public async Task RestoreProjectByIdAsync_WhenOwnerOfTheWorkspace_RestoresAndReturnsDto()
    {
        var project = AddProject("Deleted", _personal, isDeleted: true);

        var result = await _service.RestoreProjectByIdAsync(project.Id, Ct);

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == project.Id, Ct);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    // Matches the task rule: what the trash no longer lists is no longer restorable here,
    // and the admin trash is the only way back.
    [Fact]
    public async Task RestoreProjectByIdAsync_PastTheTrashWindow_ReturnsNull()
    {
        var project = AddProject(
            "Ancient",
            _personal,
            isDeleted: true,
            deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1))
        );

        Assert.Null(await _service.RestoreProjectByIdAsync(project.Id, Ct));

        var stored = await _context
            .Projects.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == project.Id, Ct);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task RestoreProjectByIdAsync_WhenAPlainMemberOfTheWorkspace_Throws403Forbidden()
    {
        var project = AddProject("Deleted", _shared, createdBy: _otherUserId, isDeleted: true);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.RestoreProjectByIdAsync(project.Id, Ct)
        );
    }

    [Fact]
    public async Task RestoreProjectByIdAsync_WhenInAWorkspaceIDoNotBelongTo_ReturnsNull()
    {
        var project = AddProject("Deleted", _foreign, createdBy: _otherUserId, isDeleted: true);

        var result = await _service.RestoreProjectByIdAsync(project.Id, Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task RestoreProjectByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.RestoreProjectByIdAsync(Guid.NewGuid(), Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task RestoreProjectByIdAsync_WhenTheWorkspaceItselfIsDeleted_Throws409Conflict()
    {
        var project = AddProject("Deleted", _personal, isDeleted: true);
        _personal.IsDeleted = true;
        _personal.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RestoreProjectByIdAsync(project.Id, Ct)
        );
    }

    [Fact]
    public async Task MoveProjectAsync_WhenOwnerOfSourceAndMemberOfTarget_Moves()
    {
        var project = AddProject("Roadmap", _personal);

        var result = await _service.MoveProjectAsync(project.Id, _shared.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(_shared.Id, result!.Project.WorkspaceId);
        var stored = await _context.Projects.FirstAsync(p => p.Id == project.Id, Ct);
        Assert.Equal(_shared.Id, stored.WorkspaceId);
    }

    [Fact]
    public async Task MoveProjectAsync_WhenAPlainMemberOfTheSource_Throws403Forbidden()
    {
        var project = AddProject("Roadmap", _shared, createdBy: _otherUserId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.MoveProjectAsync(project.Id, _personal.Id, Ct)
        );
    }

    [Fact]
    public async Task MoveProjectAsync_WhenNotAMemberOfTheTarget_Throws404NotFound()
    {
        var project = AddProject("Roadmap", _personal);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.MoveProjectAsync(project.Id, _foreign.Id, Ct)
        );
    }

    [Fact]
    public async Task MoveProjectAsync_WhenTheTargetAlreadyHasThatName_Throws409Conflict()
    {
        var project = AddProject("Roadmap", _personal);
        AddProject("Roadmap", _shared, createdBy: _otherUserId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.MoveProjectAsync(project.Id, _shared.Id, Ct)
        );
    }

    [Fact]
    public async Task MoveProjectAsync_WhenTargetIsTheCurrentWorkspace_LeavesItWhereItIs()
    {
        var project = AddProject("Roadmap", _shared, createdBy: _otherUserId);

        var result = await _service.MoveProjectAsync(project.Id, _shared.Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(_shared.Id, result!.Project.WorkspaceId);
    }

    [Fact]
    public async Task MoveProjectAsync_WhenInAWorkspaceIDoNotBelongTo_ReturnsNull()
    {
        var project = AddProject("Foreign", _foreign, createdBy: _otherUserId);

        var result = await _service.MoveProjectAsync(project.Id, _personal.Id, Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task MoveProjectAsync_UnassignsAnyoneTheTargetDoesNotContain()
    {
        var team = AddWorkspace(
            "Team",
            isPersonal: false,
            (_userId, WorkspaceRole.Owner),
            (_otherUserId, WorkspaceRole.Member)
        );
        var project = AddProject("Roadmap", team);
        var theirs = AddTask(project, _otherUserId);
        var mine = AddTask(project, _userId);

        var result = await _service.MoveProjectAsync(project.Id, _personal.Id, Ct);

        Assert.Equal(1, result!.UnassignedTaskCount);
        Assert.Null(await ReadAssigneeAsync(theirs.Id));
        Assert.Equal(_userId, await ReadAssigneeAsync(mine.Id));
    }

    [Fact]
    public async Task MoveProjectAsync_UnassignsSoftDeletedTasksToo()
    {
        var team = AddWorkspace(
            "Team",
            isPersonal: false,
            (_userId, WorkspaceRole.Owner),
            (_otherUserId, WorkspaceRole.Member)
        );
        var project = AddProject("Roadmap", team);
        var task = AddTask(project, _otherUserId, isDeleted: true);

        var result = await _service.MoveProjectAsync(project.Id, _personal.Id, Ct);

        Assert.Equal(1, result!.UnassignedTaskCount);
        Assert.Null(await ReadAssigneeAsync(task.Id));
    }

    [Fact]
    public async Task MoveProjectAsync_WhenEveryAssigneeIsAlsoInTheTarget_ChangesNothing()
    {
        var project = AddProject("Roadmap", _personal);
        var task = AddTask(project, _userId);

        var result = await _service.MoveProjectAsync(project.Id, _shared.Id, Ct);

        Assert.Equal(0, result!.UnassignedTaskCount);
        Assert.Equal(_userId, await ReadAssigneeAsync(task.Id));
    }

    public void Dispose() => _context.Dispose();
}
