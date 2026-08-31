using Backend.DTOs.User;

namespace Backend.DTOs.Dashboard;

/// <summary>
/// The platform admin's at-a-glance view: what the instance holds, what is waiting on a
/// decision, and who signed up last.
/// </summary>
/// <remarks>
/// Projects, workspaces and tasks appear only as counts — an aggregate carries no
/// workspace's content. Accounts are the admin's own domain, so those may appear as rows.
/// </remarks>
public record AdminDashboardDto
{
    // ---- Scale: live rows of each kind, one unit throughout. ----

    public required int ActiveUserCount { get; init; }

    /// <summary>Shared workspaces only — a total would track <see cref="ActiveUserCount"/>.</summary>
    public required int SharedWorkspaceCount { get; init; }
    public required int ActiveProjectCount { get; init; }
    public required int TaskCount { get; init; }

    // ---- Needs attention: every number here has a verb behind it. ----

    /// <summary>Trashed projects the purge would destroy today — it filters on this same cutoff.</summary>
    public required int PurgeableProjectCount { get; init; }

    /// <summary>Deleted but not yet anonymized: each is waiting to be restored or erased.</summary>
    public required int DeletedUserCount { get; init; }

    /// <summary>Accounts currently held by lockout, which only brute-force protection sets.</summary>
    public required int LockedOutUserCount { get; init; }

    // ---- Context, shown small. ----

    public required int DeletedProjectCount { get; init; }
    public required int DeletedWorkspaceCount { get; init; }

    /// <summary>Accounts created inside <see cref="NewUserWindowDays"/>.</summary>
    public required int NewUserCount { get; init; }

    /// <summary>How far back "new" reaches. Published so the client never has to assume it.</summary>
    public required int NewUserWindowDays { get; init; }

    /// <summary>Which instance this is; an admin acting on the wrong one is what naming it prevents.</summary>
    public required string Environment { get; init; }

    public required IReadOnlyList<UserResponseDto> RecentUsers { get; init; }
}
