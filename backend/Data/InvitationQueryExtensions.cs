using Backend.Models;

namespace Backend.Data;

public static class InvitationQueryExtensions
{
    // SQL twin of Invitation.IsPending. Change one, change the other.
    public static IQueryable<Invitation> Pending(this IQueryable<Invitation> query) =>
        query.Where(i =>
            i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow
        );
}
