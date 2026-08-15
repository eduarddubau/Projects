namespace Backend.Models;

public class WorkspaceMember
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public required WorkspaceRole Role { get; set; }
    public DateTime JoinedAt { get; set; }

    public Workspace? Workspace { get; set; }
    public User? User { get; set; }
}
