namespace Backend.Config;

/// <summary>Contract shared with the clients: these strings are matched against a
/// translation table, so renaming one is a breaking API change.</summary>
public static class BusinessRuleCodes
{
    public const string DuplicateProjectName = "DuplicateProjectName";
    public const string DuplicateEmail = "DuplicateEmail";
    public const string IdentityError = "IdentityError";

    public const string PersonalWorkspaceNotDeletable = "PersonalWorkspaceNotDeletable";
    public const string PersonalWorkspaceNotRenamable = "PersonalWorkspaceNotRenamable";
    public const string PersonalWorkspaceNoMembers = "PersonalWorkspaceNoMembers";
    public const string PersonalWorkspaceNotLeavable = "PersonalWorkspaceNotLeavable";
    public const string AlreadyWorkspaceMember = "AlreadyWorkspaceMember";

    // Used when user tries to demote or remove an owner
    public const string WorkspaceMustHaveOwner = "WorkspaceMustHaveOwner";

    // Used when an admin tries to demote or remove an owner
    public const string SoleOwnerOfWorkspaces = "SoleOwnerOfWorkspaces";

    public const string PendingInvitationExists = "PendingInvitationExists";
    public const string InvitationInvalid = "InvitationInvalid";
    public const string EmailBelongsToDeletedAccount = "EmailBelongsToDeletedAccount";
    public const string EmailReclaimed = "EmailReclaimed";
}
