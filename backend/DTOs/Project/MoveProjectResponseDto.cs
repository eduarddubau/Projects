namespace Backend.DTOs.Project;

/// <summary>The moved project, and how many of its tasks lost an assignee on the way.</summary>
public record MoveProjectResponseDto(ProjectResponseDto Project, int UnassignedTaskCount);
