using Backend.DTOs.User;

namespace Backend.DTOs.Dashboard;

/// <summary>Projects appear only as counts — an aggregate carries no workspace's content.</summary>
public record AdminDashboardDto
{
    public required int ActiveProjectCount { get; init; }
    public required int DeletedProjectCount { get; init; }
    public required int ActiveUserCount { get; init; }
    public required int DeletedUserCount { get; init; }
    public required IReadOnlyList<UserResponseDto> RecentUsers { get; init; }
}
