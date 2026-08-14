using Backend.DTOs.Project;
using Backend.Models;

namespace Backend.Mappings;

public static class ProjectMappingExtensions
{
    public static ProjectResponseDto MapToDto(this Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Description = project.Description,
        IsDeleted = project.IsDeleted,
        DeletedAt = project.DeletedAt,
        CreatedAt = project.CreatedAt,
        CreatedBy = project.CreatedBy,
        CreatedByDisplayName = project.Creator.GetDisplayName() ?? string.Empty,
        UpdatedAt = project.UpdatedAt,
        UpdatedBy = project.UpdatedBy,
        UpdatedByDisplayName = project.Updater.GetDisplayName() ?? string.Empty
    };

    public static IQueryable<ProjectResponseDto> MapToDto(this IQueryable<Project> query)
    {
        return query.Select(p => new ProjectResponseDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            IsDeleted = p.IsDeleted,
            DeletedAt = p.DeletedAt,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy,
            CreatedByDisplayName = p.Creator == null
                ? string.Empty
                : p.Creator.FirstName + " " + p.Creator.LastName,
            UpdatedAt = p.UpdatedAt,
            UpdatedBy = p.UpdatedBy,
            UpdatedByDisplayName = p.Updater == null
                ? string.Empty
                : p.Updater.FirstName + " " + p.Updater.LastName
        });
    }
}
