using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Services;

public interface IProjectService
{
    public Task<IEnumerable<ProjectResponseDto>> GetAllProjectsAsync();
    public Task<ProjectResponseDto?> GetProjectByIdAsync(Guid id);
    public Task<IEnumerable<ProjectResponseDto>> GetMyProjectsAsync();
    public Task<ProjectResponseDto> CreateProjectAsync(CreateProjectDto dto);
    public Task<bool> DeleteProjectAsync(Guid id);
}