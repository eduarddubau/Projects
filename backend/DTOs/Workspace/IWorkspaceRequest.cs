namespace Backend.DTOs.Workspace;

public interface IWorkspaceRequest
{
    string Name { get; }
    string? Description { get; }
}
