namespace Backend.DTOs.Workspace;

public record CreateWorkspaceRequest(string Name, string? Description) : IWorkspaceRequest;
