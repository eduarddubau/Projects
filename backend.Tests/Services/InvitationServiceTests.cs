using Backend.Config;
using Backend.Data;
using Backend.DTOs.Workspace;
using Backend.Exceptions;
using Backend.Models;
using Backend.Security;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services;

public sealed class InvitationServiceTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly UpperInvariantLookupNormalizer _normalizer = new();
    private readonly AppDbContext _context;
    private readonly InvitationService _service;

    private readonly User _caller = null!;

    public InvitationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        _caller = AddUser("caller@example.com", "Ada");
        _currentUser.SetupGet(c => c.UserGuid).Returns(() => _caller.Id);

        // The real guard, not a mock: the owner-only rule is part of what's under test.
        _service = new InvitationService(
            _context, _currentUser.Object, new WorkspaceAccessService(_context, _currentUser.Object), _normalizer);
    }

    private User AddUser(string email, string firstName, bool isDeleted = false, bool isAnonymized = false)
    {
        var user = new User
        {
            Email = email,
            UserName = email,
            NormalizedEmail = _normalizer.NormalizeEmail(email),
            FirstName = firstName,
            LastName = "Tester",
            IsDeleted = isDeleted,
            IsAnonymized = isAnonymized
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private Workspace AddWorkspace(string name, bool isPersonal, params (User user, WorkspaceRole role)[] members)
    {
        var workspace = new Workspace { Name = name, IsPersonal = isPersonal };

        foreach (var (user, role) in members)
            workspace.Members.Add(new WorkspaceMember
            {
                UserId = user.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });

        _context.Workspaces.Add(workspace);
        _context.SaveChanges();
        return workspace;
    }

    private Invitation AddInvitation(
        Guid workspaceId,
        string email,
        string rawToken,
        DateTime? expiresAt = null,
        DateTime? acceptedAt = null,
        DateTime? revokedAt = null)
    {
        var invitation = new Invitation
        {
            WorkspaceId = workspaceId,
            Email = email,
            NormalizedEmail = _normalizer.NormalizeEmail(email),
            Role = WorkspaceRole.Member,
            TokenHash = SecureToken.Hash(rawToken),
            InvitedBy = _caller.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(14),
            AcceptedAt = acceptedAt,
            RevokedAt = revokedAt
        };

        _context.Invitations.Add(invitation);
        _context.SaveChanges();
        return invitation;
    }

    // ---- InviteAsync: guards -------------------------------------------------------

    [Fact]
    public async Task InviteAsync_WhenCallerIsNotAMember_ThrowsNotFound()
    {
        var stranger = AddUser("stranger@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (stranger, WorkspaceRole.Owner));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.InviteAsync(workspace.Id, new InviteRequest("new@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InviteAsync_WhenCallerIsMemberbutNotOwner_ThrowsUnauthorized()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false,
            (owner, WorkspaceRole.Owner), (_caller, WorkspaceRole.Member));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.InviteAsync(workspace.Id, new InviteRequest("new@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InviteAsync_WhenWorkspaceIsPersonal_Throws()
    {
        var workspace = AddWorkspace("Ada's Workspace", isPersonal: true, (_caller, WorkspaceRole.Owner));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.InviteAsync(workspace.Id, new InviteRequest("new@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken));

        Assert.Equal(BusinessRuleCodes.PersonalWorkspaceNoMembers, ex.Code);
    }

    // ---- InviteAsync: the three-way user lookup ------------------------------------

    [Fact]
    public async Task InviteAsync_WhenEmailBelongsToLiveUser_AddsMembershipImmediately()
    {
        var invitee = AddUser("bob@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));

        var result = await _service.InviteAsync(workspace.Id, new InviteRequest("bob@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken);

        Assert.Equal(InviteOutcome.Joined, result.Outcome);
        Assert.Null(result.Token);
        Assert.NotNull(result.Member);
        Assert.True(await _context.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspace.Id && m.UserId == invitee.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await _context.Invitations.AnyAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InviteAsync_WhenEmailIsUnknown_CreatesPendingInvitationAndReturnsToken()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));

        var result = await _service.InviteAsync(workspace.Id, new InviteRequest("nobody@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken);

        Assert.Equal(InviteOutcome.Invited, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Null(result.Member);

        var invitation = await _context.Invitations.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("NOBODY@EXAMPLE.COM", invitation.NormalizedEmail);
        Assert.Equal(_caller.Id, invitation.InvitedBy);
        Assert.True(invitation.IsPending);
    }

    [Fact]
    public async Task InviteAsync_StoresOnlyTheTokenHash_NeverTheRawToken()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));

        var result = await _service.InviteAsync(workspace.Id, new InviteRequest("nobody@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken);

        var invitation = await _context.Invitations.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(result.Token, invitation.TokenHash);
        Assert.Equal(SecureToken.Hash(result.Token!), invitation.TokenHash);
    }

    [Fact]
    public async Task InviteAsync_SetsTimestampsExplicitly_BecauseNothingStampsThem()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));

        await _service.InviteAsync(workspace.Id, new InviteRequest("nobody@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken);

        var invitation = await _context.Invitations.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Invitation is not an IAuditEntity, so SaveChangesAsync skips it entirely.
        Assert.NotEqual(default, invitation.CreatedAt);
        Assert.True(invitation.ExpiresAt > invitation.CreatedAt);
    }

    [Fact]
    public async Task InviteAsync_WhenEmailBelongsToSoftDeletedUser_Throws()
    {
        AddUser("ghost@example.com", "Ghost", isDeleted: true);
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.InviteAsync(workspace.Id, new InviteRequest("ghost@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken));

        Assert.Equal(BusinessRuleCodes.EmailBelongsToDeletedAccount, ex.Code);
    }

    [Fact]
    public async Task InviteAsync_WhenEmailBelongsToAnonymizedUser_Throws()
    {
        AddUser("tombstone@example.com", "Deleted", isDeleted: true, isAnonymized: true);
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.InviteAsync(workspace.Id, new InviteRequest("tombstone@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken));

        Assert.Equal(BusinessRuleCodes.EmailBelongsToDeletedAccount, ex.Code);
    }

    // ---- InviteAsync: duplicate guards ---------------------------------------------

    [Fact]
    public async Task InviteAsync_WhenUserIsAlreadyAMember_Throws()
    {
        var member = AddUser("bob@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false,
            (_caller, WorkspaceRole.Owner), (member, WorkspaceRole.Member));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.InviteAsync(workspace.Id, new InviteRequest("bob@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken));

        Assert.Equal(BusinessRuleCodes.AlreadyWorkspaceMember, ex.Code);
    }

    [Fact]
    public async Task InviteAsync_WhenAPendingInvitationExists_Throws()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "nobody@example.com", "raw-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.InviteAsync(workspace.Id, new InviteRequest("nobody@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken));

        Assert.Equal(BusinessRuleCodes.PendingInvitationExists, ex.Code);
    }

    [Fact]
    public async Task InviteAsync_WhenPreviousInvitationWasRevoked_AllowsANewOne()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "nobody@example.com", "old-token", revokedAt: DateTime.UtcNow);

        var result = await _service.InviteAsync(workspace.Id, new InviteRequest("nobody@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken);

        Assert.Equal(InviteOutcome.Invited, result.Outcome);
        Assert.Equal(2, await _context.Invitations.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InviteAsync_WhenPreviousInvitationExpired_AllowsANewOne()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "nobody@example.com", "old-token",
            expiresAt: DateTime.UtcNow.AddDays(-1));

        var result = await _service.InviteAsync(workspace.Id, new InviteRequest("nobody@example.com", WorkspaceRole.Member), TestContext.Current.CancellationToken);

        Assert.Equal(InviteOutcome.Invited, result.Outcome);
        Assert.Equal(2, await _context.Invitations.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    // ---- GetPendingAsync / RevokeAsync ---------------------------------------------

    [Fact]
    public async Task GetPendingAsync_ExcludesRevokedExpiredAndAccepted()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "pending@example.com", "t1");
        AddInvitation(workspace.Id, "revoked@example.com", "t2", revokedAt: DateTime.UtcNow);
        AddInvitation(workspace.Id, "expired@example.com", "t3", expiresAt: DateTime.UtcNow.AddDays(-1));
        AddInvitation(workspace.Id, "accepted@example.com", "t4", acceptedAt: DateTime.UtcNow);

        var result = await _service.GetPendingAsync(workspace.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["pending@example.com"], result.Select(i => i.Email));
    }

    [Fact]
    public async Task GetPendingAsync_WhenCallerIsNotOwner_Throws()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false,
            (owner, WorkspaceRole.Owner), (_caller, WorkspaceRole.Member));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetPendingAsync(workspace.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeAsync_MarksItRevokedAndRecordsWho()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));
        var invitation = AddInvitation(workspace.Id, "nobody@example.com", "raw-token");

        await _service.RevokeAsync(workspace.Id, invitation.Id, TestContext.Current.CancellationToken);

        var stored = await _context.Invitations.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(stored.RevokedAt);
        Assert.Equal(_caller.Id, stored.RevokedBy);
        Assert.False(stored.IsPending);
    }

    [Fact]
    public async Task RevokeAsync_WhenInvitationBelongsToAnotherWorkspace_ThrowsNotFound()
    {
        var mine = AddWorkspace("Mine", isPersonal: false, (_caller, WorkspaceRole.Owner));
        var theirs = AddWorkspace("Theirs", isPersonal: false, (_caller, WorkspaceRole.Owner));
        var invitation = AddInvitation(theirs.Id, "nobody@example.com", "raw-token");

        // Owner of both, so the guard passes — only the WorkspaceId in the predicate stops this.
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RevokeAsync(mine.Id, invitation.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeAsync_WhenAlreadyRevoked_Throws()
    {
        var workspace = AddWorkspace("Acme", isPersonal: false, (_caller, WorkspaceRole.Owner));
        var invitation = AddInvitation(workspace.Id, "nobody@example.com", "t", revokedAt: DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.RevokeAsync(workspace.Id, invitation.Id, TestContext.Current.CancellationToken));

        Assert.Equal(BusinessRuleCodes.InvitationInvalid, ex.Code);
    }

    // ---- AcceptAsync ----------------------------------------------------------------

    [Fact]
    public async Task AcceptAsync_WithADifferentEmailThanInvited_StillJoins()
    {
        // This is the test that pins the bearer semantics: the token is the authorization,
        // and the caller's address is deliberately never compared to the invitation's.
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "someone-else@example.com", "raw-token");

        var result = await _service.AcceptAsync("raw-token", TestContext.Current.CancellationToken);

        Assert.Equal(workspace.Id, result.Id);
        Assert.True(await _context.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspace.Id && m.UserId == _caller.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AcceptAsync_RecordsWhoRedeemedIt()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "someone-else@example.com", "raw-token");

        await _service.AcceptAsync("raw-token", TestContext.Current.CancellationToken);

        var invitation = await _context.Invitations.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(invitation.AcceptedAt);
        Assert.Equal(_caller.Id, invitation.AcceptedBy);
    }

    [Fact]
    public async Task AcceptAsync_UsesTheInvitedRole()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));

        var invitation = AddInvitation(workspace.Id, "nobody@example.com", "raw-token");
        invitation.Role = WorkspaceRole.Owner;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.AcceptAsync("raw-token", TestContext.Current.CancellationToken);

        var member = await _context.WorkspaceMembers
            .SingleAsync(m => m.WorkspaceId == workspace.Id && m.UserId == _caller.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(WorkspaceRole.Owner, member.Role);
    }

    [Fact]
    public async Task AcceptAsync_WithAnUnknownToken_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AcceptAsync("no-such-token", TestContext.Current.CancellationToken));
        Assert.Equal(BusinessRuleCodes.InvitationInvalid, ex.Code);
    }

    [Fact]
    public async Task AcceptAsync_WithAnExpiredToken_Throws()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "nobody@example.com", "raw-token",
            expiresAt: DateTime.UtcNow.AddDays(-1));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AcceptAsync("raw-token", TestContext.Current.CancellationToken));
        Assert.Equal(BusinessRuleCodes.InvitationInvalid, ex.Code);
    }

    [Fact]
    public async Task AcceptAsync_WithARevokedToken_Throws()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "nobody@example.com", "raw-token", revokedAt: DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AcceptAsync("raw-token", TestContext.Current.CancellationToken));
        Assert.Equal(BusinessRuleCodes.InvitationInvalid, ex.Code);
    }

    [Fact]
    public async Task AcceptAsync_WhenWorkspaceIsSoftDeleted_ThrowsNotFound()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "nobody@example.com", "raw-token");

        workspace.IsDeleted = true;
        workspace.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.AcceptAsync("raw-token", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AcceptAsync_WhenAlreadyAMember_ThrowsAndLeavesTheInvitationPending()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var workspace = AddWorkspace("Acme", isPersonal: false,
            (owner, WorkspaceRole.Owner), (_caller, WorkspaceRole.Member));
        AddInvitation(workspace.Id, "nobody@example.com", "raw-token");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.AcceptAsync("raw-token", TestContext.Current.CancellationToken));

        Assert.Equal(BusinessRuleCodes.AlreadyWorkspaceMember, ex.Code);

        // Refusing must not consume the invite — revoking on a failed action would be surprising.
        var invitation = await _context.Invitations.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(invitation.IsPending);
    }

    // ---- RedeemPendingForEmailAsync -------------------------------------------------

    [Fact]
    public async Task RedeemPendingForEmailAsync_JoinsEveryWorkspaceWithAPendingInvite()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var newcomer = AddUser("newcomer@example.com", "Grace");
        var first = AddWorkspace("First", isPersonal: false, (owner, WorkspaceRole.Owner));
        var second = AddWorkspace("Second", isPersonal: false, (owner, WorkspaceRole.Owner));

        AddInvitation(first.Id, "newcomer@example.com", "t1");
        AddInvitation(second.Id, "newcomer@example.com", "t2");

        await _service.RedeemPendingForEmailAsync(newcomer, TestContext.Current.CancellationToken);

        var joined = await _context.WorkspaceMembers
            .Where(m => m.UserId == newcomer.Id)
            .Select(m => m.WorkspaceId)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, joined.Count);
        Assert.Contains(first.Id, joined);
        Assert.Contains(second.Id, joined);
        Assert.True(await _context.Invitations.AllAsync(i => i.AcceptedAt != null, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RedeemPendingForEmailAsync_IgnoresRevokedAndExpiredInvites()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var newcomer = AddUser("newcomer@example.com", "Grace");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));

        AddInvitation(workspace.Id, "newcomer@example.com", "t1", revokedAt: DateTime.UtcNow);
        AddInvitation(workspace.Id, "newcomer@example.com", "t2", expiresAt: DateTime.UtcNow.AddDays(-1));

        await _service.RedeemPendingForEmailAsync(newcomer, TestContext.Current.CancellationToken);

        Assert.False(await _context.WorkspaceMembers.AnyAsync(m => m.UserId == newcomer.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RedeemPendingForEmailAsync_SkipsSoftDeletedWorkspacesWithoutThrowing()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var newcomer = AddUser("newcomer@example.com", "Grace");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));
        AddInvitation(workspace.Id, "newcomer@example.com", "t1");

        workspace.IsDeleted = true;
        workspace.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Runs during registration, so it must never throw — skip and continue.
        await _service.RedeemPendingForEmailAsync(newcomer, TestContext.Current.CancellationToken);

        Assert.False(await _context.WorkspaceMembers.AnyAsync(m => m.UserId == newcomer.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RedeemPendingForEmailAsync_WithNoPendingInvites_DoesNothing()
    {
        var newcomer = AddUser("newcomer@example.com", "Grace");

        await _service.RedeemPendingForEmailAsync(newcomer, TestContext.Current.CancellationToken);

        Assert.False(await _context.WorkspaceMembers.AnyAsync(m => m.UserId == newcomer.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RedeemPendingForEmailAsync_WithTwoInvitesToTheSameWorkspace_AddsOneMembership()
    {
        var owner = AddUser("owner@example.com", "Bob");
        var newcomer = AddUser("newcomer@example.com", "Grace");
        var workspace = AddWorkspace("Acme", isPersonal: false, (owner, WorkspaceRole.Owner));

        // InviteAsync's guard should prevent this, but it runs unattended during signup and
        // a (WorkspaceId, UserId) unique violation there would 500 someone's registration.
        AddInvitation(workspace.Id, "newcomer@example.com", "t1");
        AddInvitation(workspace.Id, "newcomer@example.com", "t2");

        await _service.RedeemPendingForEmailAsync(newcomer, TestContext.Current.CancellationToken);

        Assert.Equal(1, await _context.WorkspaceMembers.CountAsync(m => m.UserId == newcomer.Id, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await _context.Invitations.AllAsync(i => i.AcceptedAt != null, cancellationToken: TestContext.Current.CancellationToken));
    }

    public void Dispose() => _context.Dispose();
}
