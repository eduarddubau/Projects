namespace Backend.Config;

/// <summary>Contract shared with the clients: these strings are matched against a
/// translation table, so renaming one is a breaking API change.</summary>
public static class BusinessRuleCodes
{
    public const string DuplicateProjectName = "DuplicateProjectName";
    public const string DuplicateEmail = "DuplicateEmail";
    public const string IdentityError = "IdentityError";

    /// <summary>Backstop for a length the database enforced and no validator did.
    /// Reaching this is a bug — a missing rule — but a 409 beats a 500 while it lasts.</summary>
    public const string ValueTooLong = "ValueTooLong";

    public const string PersonalWorkspaceNotDeletable = "PersonalWorkspaceNotDeletable";
    public const string PersonalWorkspaceNotRenamable = "PersonalWorkspaceNotRenamable";
    public const string PersonalWorkspaceNoMembers = "PersonalWorkspaceNoMembers";
    public const string PersonalWorkspaceNotLeavable = "PersonalWorkspaceNotLeavable";
    public const string AlreadyWorkspaceMember = "AlreadyWorkspaceMember";
    public const string WorkspaceHasProjects = "WorkspaceHasProjects";
    public const string WorkspaceIsDeleted = "WorkspaceIsDeleted";

    // Used when user tries to demote or remove an owner
    public const string WorkspaceMustHaveOwner = "WorkspaceMustHaveOwner";

    // Used when an admin tries to demote or remove an owner
    public const string SoleOwnerOfWorkspaces = "SoleOwnerOfWorkspaces";

    public const string PendingInvitationExists = "PendingInvitationExists";
    public const string InvitationInvalid = "InvitationInvalid";
    public const string EmailBelongsToDeletedAccount = "EmailBelongsToDeletedAccount";
    public const string EmailReclaimed = "EmailReclaimed";
}
