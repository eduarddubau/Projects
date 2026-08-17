namespace Backend.DTOs.Dashboard;

/// <summary>The at-a-glance numbers for one workspace's home.</summary>
// Both counts exclude Done deliberately — a lifetime total only ever grows, so it stops
// being something to act on. Same unit either side, so the pair reads as "the workspace's
// load, and my share of it".
public record WorkspaceDashboardDto
{
    /// <summary>Unfinished tasks across every project in the workspace.</summary>
    public required int OpenTaskCount { get; init; }

    /// <summary>Of those, the ones assigned to the caller.</summary>
    public required int MyOpenTaskCount { get; init; }
}
