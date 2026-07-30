namespace Backend.Config;

/// <summary>Contract shared with the clients: these strings are matched against a
/// translation table, so renaming one is a breaking API change.</summary>
public static class BusinessRuleCodes
{
    public const string DuplicateProjectName = "DuplicateProjectName";
    public const string DuplicateEmail = "DuplicateEmail";
    public const string IdentityError = "IdentityError";

    public const string PersonalWorkspaceNotDeletable = "PersonalWorkspaceNotDeletable";
    public const string PersonalWorkspaceNoMembers = "PersonalWorkspaceNoMembers";
    public const string PersonalWorkspaceNotLeavable = "PersonalWorkspaceNotLeavable";
    public const string AlreadyWorkspaceMember = "AlreadyWorkspaceMember";
    public const string WorkspaceMustHaveOwner = "WorkspaceMustHaveOwner";
}
