using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ProjectService : BaseService<Project>, IProjectService
{
    public ProjectService(AppDbContext context, ICurrentUserService currentUser) 
        : base(context, currentUser) { }

    public async Task<Project?> GetProjectByIdAsync(Guid id)
    {
        return await GetByIdSecureAsync(id); 
    }

    public async Task<List<Project>> GetMyProjectsAsync()
    {
        if (_currentUser.IsAdmin)
        {
            return await _context.Projects.ToListAsync();
        }

        return await _context.Projects
            .Where(p => p.CreatedBy == _currentUser.UserId)
            .ToListAsync();
    }

    public async Task<Project> CreateProjectAsync(CreateProjectDto dto)
    {
        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description ?? string.Empty
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project;
    }
}