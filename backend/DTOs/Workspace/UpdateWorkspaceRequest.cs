namespace Backend.DTOs.Workspace;

public record UpdateWorkspaceRequest(string Name, string? Description) : IWorkspaceRequest;
