using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Services;

public interface IProjectService
{
    public Task<Project?> GetProjectByIdAsync(Guid id);
    public Task<List<Project>> GetMyProjectsAsync();
    public Task<Project> CreateProjectAsync(CreateProjectDto dto);
}