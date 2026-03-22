using Backend.DTOs;
using Backend.Models;

namespace Backend.Mappings;

public static class ProjectMappingExtensions
{
    public static ProjectResponseDto MapToDto(this Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Description = project.Description,
        CreatedAt = project.CreatedAt,
        CreatedBy = project.CreatedBy,
        CreatedByDisplayName = project.Creator.GetDisplayName() ?? string.Empty,
        UpdatedAt = project.UpdatedAt,
        UpdatedBy = project.UpdatedBy,
        UpdatedByDisplayName = project.Updater.GetDisplayName() ?? string.Empty
    };

    public static IQueryable<ProjectResponseDto> ProjectToDto(this IQueryable<Project> query)
    {
        return query.Select(p => new ProjectResponseDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy,
            CreatedByDisplayName = p.Creator == null
                ? string.Empty
                : (p.Creator.FirstName + " " + p.Creator.LastName).Trim() != ""
                    ? (p.Creator.FirstName + " " + p.Creator.LastName).Trim()
                    : p.Creator.Email ?? string.Empty,
            UpdatedAt = p.UpdatedAt,
            UpdatedBy = p.UpdatedBy,
            UpdatedByDisplayName = p.Updater == null
                ? string.Empty
                : (p.Updater.FirstName + " " + p.Updater.LastName).Trim() != ""
                    ? (p.Updater.FirstName + " " + p.Updater.LastName).Trim()
                    : p.Updater.Email ?? string.Empty
        });
    }
}