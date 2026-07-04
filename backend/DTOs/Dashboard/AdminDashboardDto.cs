using Backend.DTOs.Project;
using Backend.DTOs.User;

namespace Backend.DTOs.Dashboard;

public record AdminDashboardDto
{
    public required int ActiveProjectCount { get; init; }
    public required int DeletedProjectCount { get; init; }
    public required int ActiveUserCount { get; init; }
    public required int DeletedUserCount { get; init; }
    public required IReadOnlyList<ProjectResponseDto> RecentProjects { get; init; }
    public required IReadOnlyList<UserResponseDto> RecentUsers { get; init; }
}
