using Backend.Data;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Mappings;

/// <summary>
/// No projection names a soft-deleted person, whatever the query filters are doing.
///
/// Every trash in the app reads through IgnoreQueryFilters(), which is query-wide in EF and
/// switches the User filter off with everything else — so each projection has to test
/// IsDeleted itself. One test per projection, because the first pass at this guarded only
/// the task mappers and left the project, workspace and user ones leaking.
///
/// Each fixture sets CreatedBy/UpdatedBy explicitly. Without that the navigations resolve to
/// null, the `== null` branch answers first, and every assertion here passes with the guards
/// deleted — which is exactly how the first version of these tests proved nothing.
/// </summary>
public sealed class SoftDeletedNameTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly User _ghost;
    private readonly Workspace _workspace;
    private readonly Project _project;

    public SoftDeletedNameTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        _ghost = new User
        {
            FirstName = "Ghost",
            LastName = "User",
            Email = "ghost@example.com",
        };
        // A separate creator, because CreatedByDisplayName on a user is whoever created
        // *them*. Ids are assigned by EF on Add(), not by the constructor — User extends
        // IdentityUser<Guid>, which leaves Id default — so admin.Id is only real after this.
        var admin = new User
        {
            FirstName = "Admin",
            LastName = "Person",
            Email = "admin@example.com",
        };
        _context.Users.AddRange(admin, _ghost);

        _ghost.CreatedBy = admin.Id;
        _ghost.UpdatedBy = admin.Id;
        _ghost.UpdatedAt = DateTime.UtcNow;

        _workspace = new Workspace
        {
            Name = "Acme",
            IsPersonal = false,
            CreatedBy = _ghost.Id,
            UpdatedBy = _ghost.Id,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Workspaces.Add(_workspace);
        _context.SaveChanges();

        _project = new Project
        {
            Name = "Rocket Plans",
            WorkspaceId = _workspace.Id,
            CreatedBy = _ghost.Id,
            UpdatedBy = _ghost.Id,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Projects.Add(_project);

        _context.WorkspaceMembers.Add(
            new WorkspaceMember
            {
                WorkspaceId = _workspace.Id,
                UserId = _ghost.Id,
                Role = WorkspaceRole.Owner,
                JoinedAt = DateTime.UtcNow,
            }
        );

        _context.Invitations.Add(
            new Invitation
            {
                WorkspaceId = _workspace.Id,
                Email = "invitee@example.com",
                NormalizedEmail = "INVITEE@EXAMPLE.COM",
                Role = WorkspaceRole.Member,
                TokenHash = "hash",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                InvitedBy = _ghost.Id,
            }
        );

        _context.Tasks.Add(
            new TaskItem
            {
                Title = "Rebuild the homepage",
                ProjectId = _project.Id,
                AssigneeId = _ghost.Id,
                CreatedBy = _ghost.Id,
                UpdatedBy = _ghost.Id,
                UpdatedAt = DateTime.UtcNow,
            }
        );

        // Synchronous: SaveChangesAsync carries the audit interceptor, which would overwrite
        // the CreatedBy/UpdatedBy this fixture depends on.
        _context.SaveChanges();

        SetEveryoneDeleted(true);
    }

    // Proves the fixture can fail: with the person alive every projection names them, so a
    // green run below is the guard working rather than a navigation that was null all along.
    [Fact]
    public void TheFixtureNamesThePersonWhileTheyAreAlive()
    {
        SetEveryoneDeleted(false);

        Assert.Equal("Ghost User", ProjectDto().CreatedByDisplayName);
        Assert.Equal("Ghost User", WorkspaceDto().CreatedByDisplayName);
        Assert.Equal("Ghost User", AdminWorkspaceDto().CreatedByDisplayName);
        Assert.Equal("Admin Person", UserDto().CreatedByDisplayName);
        Assert.Equal(
            "Ghost User",
            _context
                .Projects.IgnoreQueryFilters()
                .Include(p => p.Creator)
                .First()
                .Creator.GetDisplayName()
        );
        Assert.Equal("Ghost User", MemberDto().UserDisplayName);
        Assert.Equal("Ghost User", InvitationDto().InvitedByDisplayName);
        Assert.Equal("Ghost User", TaskDto().AssigneeDisplayName);
    }

    private void SetEveryoneDeleted(bool deleted)
    {
        foreach (var user in _context.Users.IgnoreQueryFilters())
        {
            user.IsDeleted = deleted;
            user.DeletedAt = deleted ? DateTime.UtcNow : null;
        }

        _context.SaveChanges();
    }

    [Fact]
    public void TheProjectTrashDoesNotNameThem() =>
        Assert.Equal(string.Empty, ProjectDto().CreatedByDisplayName);

    [Fact]
    public void TheWorkspaceTrashDoesNotNameThem() =>
        Assert.Equal(string.Empty, WorkspaceDto().CreatedByDisplayName);

    // The one the earlier test missed: the admin workspace trash reads MapToAdminDto, and
    // the assertion was pointed at MapToDto instead.
    [Fact]
    public void TheAdminWorkspaceTrashDoesNotNameThem() =>
        Assert.Equal(string.Empty, AdminWorkspaceDto().CreatedByDisplayName);

    [Fact]
    public void TheAdminUserListDoesNotNameThem() =>
        Assert.Equal(string.Empty, UserDto().CreatedByDisplayName);

    [Fact]
    public void TheMemberListDoesNotNameThem() =>
        Assert.Equal(string.Empty, MemberDto().UserDisplayName);

    [Fact]
    public void TheInvitationListDoesNotNameThem() =>
        Assert.Equal(string.Empty, InvitationDto().InvitedByDisplayName);

    /// <summary>
    /// The entity half of the same invariant. Every getter above goes through an IQueryable
    /// overload, so `GetDisplayName`'s own guard was asserted nowhere — reverting it left the
    /// whole suite green. These are the live paths that reach it: an admin opening a
    /// soft-deleted user, and any detail read that Includes Creator under IgnoreQueryFilters.
    /// </summary>
    [Fact]
    public void TheLoadedEntityPathDoesNotNameThemEither()
    {
        var project = _context
            .Projects.IgnoreQueryFilters()
            .Include(p => p.Creator)
            .Include(p => p.Updater)
            .First();

        var dto = project.MapToDto();

        Assert.Equal(string.Empty, dto.CreatedByDisplayName);
        Assert.Equal(string.Empty, dto.UpdatedByDisplayName);
    }

    [Fact]
    public void GetDisplayNameItselfRefusesADeletedPerson()
    {
        var ghost = _context.Users.IgnoreQueryFilters().First(u => u.Email == "ghost@example.com");

        Assert.True(ghost.IsDeleted);
        Assert.Null(ghost.GetDisplayName());
    }

    [Fact]
    public void TheTaskTrashDoesNotNameThem()
    {
        var task = TaskDto();

        Assert.Null(task.AssigneeDisplayName);
        Assert.Equal(string.Empty, task.CreatedByDisplayName);
        Assert.Equal(string.Empty, task.UpdatedByDisplayName);
    }

    private DTOs.Project.ProjectResponseDto ProjectDto() =>
        _context.Projects.IgnoreQueryFilters().MapToDto().First();

    private DTOs.Workspace.WorkspaceResponseDto WorkspaceDto() =>
        _context.Workspaces.IgnoreQueryFilters().MapToDto(null).First();

    private DTOs.Workspace.AdminWorkspaceResponseDto AdminWorkspaceDto() =>
        _context.Workspaces.IgnoreQueryFilters().MapToAdminDto().First();

    private DTOs.User.UserResponseDto UserDto() =>
        _context
            .Users.IgnoreQueryFilters()
            .Where(u => u.Email == "ghost@example.com")
            .MapToDto()
            .First();

    private DTOs.Workspace.WorkspaceMemberResponseDto MemberDto() =>
        _context.WorkspaceMembers.IgnoreQueryFilters().MapToDto().First();

    private DTOs.Workspace.InvitationResponseDto InvitationDto() =>
        _context.Invitations.IgnoreQueryFilters().MapToDto().First();

    private DTOs.Task.TaskResponseDto TaskDto() =>
        _context.Tasks.IgnoreQueryFilters().MapToDto().First();

    public void Dispose() => _context.Dispose();
}
