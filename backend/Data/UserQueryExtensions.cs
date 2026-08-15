using Backend.Config;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public static class UserQueryExtensions
{
    /// <summary>Membership in the Admin role, which bars an account from holding
    /// projects or joining workspaces.</summary>
    public static Task<bool> IsAdminAsync(
        this AppDbContext context,
        Guid userId,
        CancellationToken ct = default
    ) =>
        context
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(context.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .AnyAsync(name => name == AppRoles.Admin, ct);
}
