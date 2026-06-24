namespace Backend.DTOs.Project;

public interface IProjectRequest
{
    string Name { get; }
    string? Description { get; }
}
