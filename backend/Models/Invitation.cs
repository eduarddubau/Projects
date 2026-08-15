using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required WorkspaceRole Role { get; set; }
    public required string TokenHash { get; set; }
    public Guid InvitedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    // Bearer semantics: whoever holds the link may redeem it, so this is not
    // necessarily the user matching Email.
    public Guid? AcceptedBy { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }

    // Not queryable — see InvitationQueryExtensions.Pending().
    public bool IsPending => AcceptedAt is null && RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    public Workspace? Workspace { get; set; }

    [ForeignKey("InvitedBy")]
    public User? Inviter { get; set; }
}
