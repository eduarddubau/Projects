namespace Backend.Models;

public class WorkspaceMember
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public required WorkspaceRole Role { get; set; }
    public DateTime JoinedAt { get; set; }

    public virtual Workspace? Workspace { get; set; }
    public virtual User? User { get; set; }
}
