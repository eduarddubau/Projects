namespace Backend.Models;

// Member is first so the zero value is the least-privileged role.
public enum WorkspaceRole
{
    Member,
    Owner,
}
