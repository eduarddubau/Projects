using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Services;

public interface IProjectService
{
    // Standard user methods
    Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync();
    Task<ProjectResponseDto?> GetMyProjectByIdAsync(Guid id);
    Task<ProjectResponseDto> CreateProjectAsync(CreateProjectDto dto);
    Task<bool> DeleteMyProjectAsync(Guid id);
    Task<ProjectResponseDto?> UpdateMyProjectAsync(Guid id, UpdateProjectDto dto);

    // Admin methods
    Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync();
    Task<ProjectResponseDto?> GetAnyProjectByIdAsync(Guid id);
    Task<bool> DeleteAnyProjectAsync(Guid id);
    Task<ProjectResponseDto?> RestoreAnyProjectAsync(Guid projectId);
    Task<IEnumerable<ProjectResponseDto>> GetDeletedProjectsAsync();
}