using Backend.Config;
using Backend.Data;
using Backend.DTOs.Workspace;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services;

public sealed class WorkspaceServiceTests : IDisposable
{
    private static readonly string[] ExpectedWorkspaceNames = ["Ada's Workspace", "Alpha", "Zulu"];

    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly WorkspaceService _service;

    private readonly User _caller = null!;

    public WorkspaceServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        _caller = AddUser("caller@example.com", "Ada");
        _currentUser.SetupGet(c => c.UserGuid).Returns(() => _caller.Id);

        // The real guard, not a mock: the 404-vs-403 split is the behaviour under test.
        _service = new WorkspaceService(
            _context,
            _currentUser.Object,
            new WorkspaceAccessService(_context, _currentUser.Object)
        );
    }

    private User AddUser(string email, string firstName, string? nickname = null)
    {
        var user = new User
        {
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = "Tester",
            Nickname = nickname,
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private Workspace AddWorkspace(
        string name,
        bool isPersonal,
        params (User user, WorkspaceRole role)[] members
    )
    {
        var workspace = new Workspace { Name = name, IsPersonal = isPersonal };

        foreach (var (user, role) in members)
            workspace.Members.Add(
                new WorkspaceMember
                {
                    UserId = user.Id,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                }
            );

        _context.Workspaces.Add(workspace);
        _context.SaveChanges();
        return workspace;
    }

    [Fact]
    public async Task EnsurePersonalWorkspaceAsync_WithAMaxLengthFirstName_FitsTheColumn()
    {
        // FirstName is validated at 50 and "'s Workspace" costs 12, so an untruncated name
        // reaches 62 against a 60-char column -- Postgres 22001, surfacing as a 500 on
        // registration. InMemory won't enforce the length, so assert it directly.
        var user = AddUser("long@example.com", new string('A', 50));

        await _service.EnsurePersonalWorkspaceAsync(user, TestContext.Current.CancellationToken);

        var workspace = await _context.Workspaces.SingleAsync(
            w => w.IsPersonal && w.CreatedBy == user.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.True(
            workspace.Name.Length <= Workspace.NameMaxLength,
            $"derived name was {workspace.Name.Length} chars, column allows {Workspace.NameMaxLength}"
        );
        Assert.EndsWith("'s Workspace", workspace.Name);
    }

    // ---- scoping -------------------------------------------------------------------

    [Fact]
    public async Task GetMyWorkspacesAsync_ReturnsOnlyMine_PersonalFirstThenByName()
    {
        var stranger = AddUser("stranger@example.com", "Bob");

        AddWorkspace("Zulu", isPersonal: false, (_caller, WorkspaceRole.Member));
        AddWorkspace("Alpha", isPersonal: false, (_caller, WorkspaceRole.Owner));
        AddWorkspace("Ada's Workspace", isPersonal: true, (_caller, WorkspaceRole.Owner));
        AddWorkspace("Not Mine", isPersonal: false, (stranger, WorkspaceRole.Owner));

        var result = (
            await _service.GetMyWorkspacesAsync(TestContext.Current.CancellationToken)
        ).ToList();

        Assert.Equal(ExpectedWorkspaceNames, result.Select(w => w.Name));
        Assert.DoesNotContain(result, w => w.Name == "Not Mine");
    }

    [Fact]
    public async Task GetWorkspaceByIdAsync_WhenNotAMember_ReturnsNullRatherThanLeaking()
    {
        var stranger = AddUser("stranger@example.com", "Bob");
        var theirs = AddWorkspace("Theirs", isPersonal: false, (stranger, WorkspaceRole.Owner));

        Assert.Null(
            await _service.GetWorkspaceByIdAsync(theirs.Id, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task GetMyWorkspacesAsync_ReportsMyRoleAndMemberCount()
    {
        var other = AddUser("other@example.com", "Bob");
        AddWorkspace(
            "Shared",
            isPersonal: false,
            (_caller, WorkspaceRole.Member),
            (other, WorkspaceRole.Owner)
        );

        var shared = Assert.Single(
            await _service.GetMyWorkspacesAsync(TestContext.Current.CancellationToken)
        );

        Assert.Equal(WorkspaceRole.Member, shared.MyRole);
        Assert.Equal(2, shared.MemberCount);
    }

    // ---- create --------------------------------------------------------------------

    [Fact]
    public async Task CreateWorkspaceAsync_MakesTheCallerAnOwner()
    {
        var created = await _service.CreateWorkspaceAsync(
            new CreateWorkspaceRequest("Acme Team", "Shared."),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WorkspaceRole.Owner, created.MyRole);
        Assert.Equal(1, created.MemberCount);
        Assert.False(created.IsPersonal);

        // Without the membership row the creator cannot see what they just made.
        Assert.Single(
            await _context
                .WorkspaceMembers.Where(m => m.WorkspaceId == created.Id && m.UserId == _caller.Id)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    // ---- the 404-vs-403 split ------------------------------------------------------

    [Fact]
    public async Task UpdateWorkspaceAsync_WhenNotAMember_Throws404NotFound()
    {
        var stranger = AddUser("stranger@example.com", "Bob");
        var theirs = AddWorkspace("Theirs", isPersonal: false, (stranger, WorkspaceRole.Owner));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateWorkspaceAsync(
                theirs.Id,
                new UpdateWorkspaceRequest("Hijacked", null),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_WhenMemberButNotOwner_Throws403Forbidden()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var shared = AddWorkspace(
            "Shared",
            isPersonal: false,
            (owner, WorkspaceRole.Owner),
            (_caller, WorkspaceRole.Member)
        );

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpdateWorkspaceAsync(
                shared.Id,
                new UpdateWorkspaceRequest("Renamed", null),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_WhenPersonal_IsRefused()
    {
        // Clients render personal workspaces from a translation key, not from this column,
        // so a rename would be accepted and then never displayed.
        var personal = AddWorkspace(
            "Ada's Workspace",
            isPersonal: true,
            (_caller, WorkspaceRole.Owner)
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateWorkspaceAsync(
                personal.Id,
                new UpdateWorkspaceRequest("Renamed", null),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(BusinessRuleCodes.PersonalWorkspaceNotRenamable, ex.Code);

        var stored = await _context.Workspaces.SingleAsync(
            w => w.Id == personal.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal("Ada's Workspace", stored.Name);
    }

    // ---- delete --------------------------------------------------------------------

    [Fact]
    public async Task DeleteWorkspaceAsync_WhenPersonal_IsRefused()
    {
        var personal = AddWorkspace(
            "Ada's Workspace",
            isPersonal: true,
            (_caller, WorkspaceRole.Owner)
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.DeleteWorkspaceAsync(personal.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.PersonalWorkspaceNotDeletable, ex.Code);
        Assert.Single(
            await _context
                .Workspaces.Where(w => w.Id == personal.Id)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
    }

    private Project AddProject(string name, Guid workspaceId, bool trashed = false)
    {
        var project = new Project
        {
            Name = name,
            WorkspaceId = workspaceId,
            CreatedBy = _caller.Id,
        };

        _context.Projects.Add(project);
        _context.SaveChanges();

        if (trashed)
        {
            // SaveChanges forces IsDeleted = false on Added, so trash it in a second pass.
            project.IsDeleted = true;
            project.DeletedAt = DateTime.UtcNow;
            _context.SaveChanges();
        }

        return project;
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_WhenItHoldsProjects_IsRefused()
    {
        var shared = AddWorkspace("Acme Team", isPersonal: false, (_caller, WorkspaceRole.Owner));
        AddProject("Live one", shared.Id);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.DeleteWorkspaceAsync(shared.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.WorkspaceHasProjects, ex.Code);
    }

    // The IgnoreQueryFilters case: without it the guard sees an empty workspace and the
    // project comes back, on restore, into somewhere no member can reach.
    [Fact]
    public async Task DeleteWorkspaceAsync_WhenItHoldsOnlyTrashedProjects_IsStillRefused()
    {
        var shared = AddWorkspace("Acme Team", isPersonal: false, (_caller, WorkspaceRole.Owner));
        AddProject("Trashed one", shared.Id, trashed: true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.DeleteWorkspaceAsync(shared.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.WorkspaceHasProjects, ex.Code);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_SoftDeletesRatherThanPurging()
    {
        var shared = AddWorkspace("Acme Team", isPersonal: false, (_caller, WorkspaceRole.Owner));

        await _service.DeleteWorkspaceAsync(shared.Id, TestContext.Current.CancellationToken);

        Assert.Empty(
            await _context
                .Workspaces.Where(w => w.Id == shared.Id)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.True(
            (
                await _context
                    .Workspaces.IgnoreQueryFilters()
                    .FirstAsync(
                        w => w.Id == shared.Id,
                        cancellationToken: TestContext.Current.CancellationToken
                    )
            ).IsDeleted
        );
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_WithMembersLoaded_KeepsTheMembershipRows()
    {
        var member = AddUser("member@example.com", "Bob");
        var shared = AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (_caller, WorkspaceRole.Owner),
            (member, WorkspaceRole.Member)
        );

        // The membership rows are tracked here. Remove() would cascade and hard-delete them,
        // leaving a soft-deleted workspace with no owner — absent from trash and unrestorable.
        await _service.DeleteWorkspaceAsync(shared.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            await _context.WorkspaceMembers.CountAsync(
                m => m.WorkspaceId == shared.Id,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(
            "Acme Team",
            Assert
                .Single(
                    await _service.GetDeletedWorkspacesAsync(TestContext.Current.CancellationToken)
                )
                .Name
        );
    }

    [Fact]
    public async Task RestoreWorkspaceAsync_BringsItBackWithItsMembers()
    {
        var member = AddUser("member@example.com", "Bob");
        var shared = AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (_caller, WorkspaceRole.Owner),
            (member, WorkspaceRole.Member)
        );

        await _service.DeleteWorkspaceAsync(shared.Id, TestContext.Current.CancellationToken);
        var restored = await _service.RestoreWorkspaceAsync(
            shared.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, restored.MemberCount);
        Assert.Equal(WorkspaceRole.Owner, restored.MyRole);
        Assert.False(restored.IsDeleted);
    }

    // ---- members -------------------------------------------------------------------

    [Fact]
    public async Task AddMemberAsync_ToAPersonalWorkspace_IsRefused()
    {
        var outsider = AddUser("outsider@example.com", "Bob");
        var personal = AddWorkspace(
            "Ada's Workspace",
            isPersonal: true,
            (_caller, WorkspaceRole.Owner)
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.AddMemberAsync(
                personal.Id,
                new AddMemberRequest(outsider.Id, WorkspaceRole.Member),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(BusinessRuleCodes.PersonalWorkspaceNoMembers, ex.Code);
    }

    [Fact]
    public async Task AddMemberAsync_WhenAlreadyAMember_IsRefused()
    {
        var member = AddUser("member@example.com", "Bob");
        var shared = AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (_caller, WorkspaceRole.Owner),
            (member, WorkspaceRole.Member)
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.AddMemberAsync(
                shared.Id,
                new AddMemberRequest(member.Id, WorkspaceRole.Member),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(BusinessRuleCodes.AlreadyWorkspaceMember, ex.Code);
    }

    [Fact]
    public async Task AddMemberAsync_WhenTheUserDoesNotExist_Throws404()
    {
        var shared = AddWorkspace("Acme Team", isPersonal: false, (_caller, WorkspaceRole.Owner));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddMemberAsync(
                shared.Id,
                new AddMemberRequest(Guid.NewGuid(), WorkspaceRole.Member),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsTheMemberWithTheirDisplayName()
    {
        var invitee = AddUser("bob@example.com", "Bob");
        var shared = AddWorkspace("Acme Team", isPersonal: false, (_caller, WorkspaceRole.Owner));

        var member = await _service.AddMemberAsync(
            shared.Id,
            new AddMemberRequest(invitee.Id, WorkspaceRole.Member),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(invitee.Id, member.UserId);
        Assert.Equal(WorkspaceRole.Member, member.Role);
        Assert.Equal("Bob Tester", member.UserDisplayName);
    }

    // ---- the last-owner invariant, across all three ways to break it ---------------

    [Fact]
    public async Task ChangeRoleAsync_DemotingTheLastOwner_IsRefused()
    {
        var member = AddUser("member@example.com", "Bob");
        var shared = AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (_caller, WorkspaceRole.Owner),
            (member, WorkspaceRole.Member)
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.ChangeRoleAsync(
                shared.Id,
                _caller.Id,
                WorkspaceRole.Member,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(BusinessRuleCodes.WorkspaceMustHaveOwner, ex.Code);
    }

    [Fact]
    public async Task RemoveMemberAsync_RemovingTheLastOwner_IsRefused()
    {
        var shared = AddWorkspace("Acme Team", isPersonal: false, (_caller, WorkspaceRole.Owner));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RemoveMemberAsync(shared.Id, _caller.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.WorkspaceMustHaveOwner, ex.Code);
    }

    [Fact]
    public async Task LeaveAsync_AsTheLastOwner_IsRefused()
    {
        var member = AddUser("member@example.com", "Bob");
        var shared = AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (_caller, WorkspaceRole.Owner),
            (member, WorkspaceRole.Member)
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.LeaveAsync(shared.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.WorkspaceMustHaveOwner, ex.Code);
    }

    [Fact]
    public async Task ChangeRoleAsync_DemotingAnOwnerWhileAnotherRemains_Succeeds()
    {
        // A workspace may hold many owners; only reaching zero is forbidden.
        var coOwner = AddUser("co@example.com", "Bob");
        var shared = AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (_caller, WorkspaceRole.Owner),
            (coOwner, WorkspaceRole.Owner)
        );

        var demoted = await _service.ChangeRoleAsync(
            shared.Id,
            coOwner.Id,
            WorkspaceRole.Member,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WorkspaceRole.Member, demoted.Role);
    }

    [Fact]
    public async Task LeaveAsync_AsAPlainMember_RemovesOnlyThem()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var shared = AddWorkspace(
            "Acme Team",
            isPersonal: false,
            (owner, WorkspaceRole.Owner),
            (_caller, WorkspaceRole.Member)
        );

        await _service.LeaveAsync(shared.Id, TestContext.Current.CancellationToken);

        var remaining = await _context
            .WorkspaceMembers.Where(m => m.WorkspaceId == shared.Id)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(owner.Id, Assert.Single(remaining).UserId);
    }

    [Fact]
    public async Task LeaveAsync_FromAPersonalWorkspace_IsRefused()
    {
        var personal = AddWorkspace(
            "Ada's Workspace",
            isPersonal: true,
            (_caller, WorkspaceRole.Owner)
        );

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.LeaveAsync(personal.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(BusinessRuleCodes.PersonalWorkspaceNotLeavable, ex.Code);
    }

    // ---- personal workspaces -------------------------------------------------------

    [Fact]
    public async Task EnsurePersonalWorkspaceAsync_IsIdempotent()
    {
        await _service.EnsurePersonalWorkspaceAsync(_caller, TestContext.Current.CancellationToken);
        await _service.EnsurePersonalWorkspaceAsync(_caller, TestContext.Current.CancellationToken);

        Assert.Equal(
            1,
            await _context.Workspaces.CountAsync(
                w => w.IsPersonal,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task EnsurePersonalWorkspaceAsync_PrefersNicknameAndOwnsIt()
    {
        var user = AddUser("grace@example.com", "Grace", nickname: "Amazing");

        await _service.EnsurePersonalWorkspaceAsync(user, TestContext.Current.CancellationToken);

        var workspace = await _context
            .Workspaces.Include(w => w.Members)
            .FirstAsync(
                w => w.IsPersonal,
                cancellationToken: TestContext.Current.CancellationToken
            );

        Assert.Equal("Amazing's Workspace", workspace.Name);
        Assert.Equal(user.Id, workspace.CreatedBy);
        Assert.Equal(WorkspaceRole.Owner, Assert.Single(workspace.Members).Role);
    }

    [Fact]
    public async Task GetDeletedWorkspacesAsync_ShowsOnlyOnesIOwned()
    {
        var stranger = AddUser("stranger@example.com", "Bob");
        var mine = AddWorkspace("Mine", isPersonal: false, (_caller, WorkspaceRole.Owner));
        var asMember = AddWorkspace(
            "AsMember",
            isPersonal: false,
            (stranger, WorkspaceRole.Owner),
            (_caller, WorkspaceRole.Member)
        );

        await _service.DeleteWorkspaceAsync(mine.Id, TestContext.Current.CancellationToken);

        // The stranger's workspace, soft-deleted the same way the service does it.
        asMember.IsDeleted = true;
        asMember.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var trash = await _service.GetDeletedWorkspacesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Mine", Assert.Single(trash).Name);
    }

    public void Dispose() => _context.Dispose();
}
