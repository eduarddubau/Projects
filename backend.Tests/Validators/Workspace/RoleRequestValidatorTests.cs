using Backend.DTOs.Workspace;
using Backend.Models;
using Backend.Validators.Workspace;

namespace Backend.Tests.Validators.Workspace;

public class RoleRequestValidatorTests
{
    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Owner)]
    public void AddMemberRequest_AcceptsAnyRealRole(WorkspaceRole role)
    {
        var result = new AddMemberRequestValidator().Validate(
            new AddMemberRequest(Guid.NewGuid(), role)
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AddMemberRequest_RejectsAnUndefinedNumericRole()
    {
        var result = new AddMemberRequestValidator().Validate(
            new AddMemberRequest(Guid.NewGuid(), (WorkspaceRole)99)
        );

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddMemberRequest.Role));
    }

    [Fact]
    public void AddMemberRequest_RejectsAnEmptyUserId()
    {
        var result = new AddMemberRequestValidator().Validate(
            new AddMemberRequest(Guid.Empty, WorkspaceRole.Member)
        );

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddMemberRequest.UserId));
    }

    [Fact]
    public void AddMemberRequest_AllowsAnOmittedRole()
    {
        var result = new AddMemberRequestValidator().Validate(
            new AddMemberRequest(Guid.NewGuid(), null)
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public void InviteRequest_AllowsAnOmittedRole()
    {
        var result = new InviteRequestValidator().Validate(
            new InviteRequest("someone@example.com", null)
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ChangeMemberRoleRequest_RejectsAnUndefinedNumericRole()
    {
        var result = new ChangeMemberRoleRequestValidator().Validate(
            new ChangeMemberRoleRequest((WorkspaceRole)99)
        );

        Assert.False(result.IsValid);
    }

    // The under-posting guard: {} would otherwise bind to the enum's zero value and
    // silently demote an owner to Member.
    [Fact]
    public void ChangeMemberRoleRequest_RejectsAnOmittedRole()
    {
        var result = new ChangeMemberRoleRequestValidator().Validate(
            new ChangeMemberRoleRequest(null)
        );

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ChangeMemberRoleRequest.Role));
    }

    [Theory]
    [InlineData(WorkspaceRole.Member)]
    [InlineData(WorkspaceRole.Owner)]
    public void ChangeMemberRoleRequest_AcceptsAnyRealRole(WorkspaceRole role)
    {
        var result = new ChangeMemberRoleRequestValidator().Validate(
            new ChangeMemberRoleRequest(role)
        );

        Assert.True(result.IsValid);
    }
}
