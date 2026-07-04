using Backend.DTOs.Project;

namespace Backend.DTOs.Dashboard;

public record UserDashboardDto
{
    public required int ActiveProjectCount { get; init; }
    public required int DeletedProjectCount { get; init; }
    public DateTime? LastActivityAt { get; init; }
    public required IReadOnlyList<ProjectResponseDto> RecentProjects { get; init; }
}
