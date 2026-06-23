using Backend.Data;
using Backend.DTOs.Project;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Services;

public class ProjectServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly ProjectService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public ProjectServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);
        _service = new ProjectService(_context, _currentUser.Object);

        _currentUser.Setup(c => c.UserGuid).Returns(_userId);
    }

    private Project AddProject(string name, Guid createdBy, bool isDeleted = false)
    {
        var project = new Project { Name = name, CreatedBy = createdBy, IsDeleted = isDeleted };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    [Fact]
    public async Task GetMyProjectsAsync_ReturnsOnlyCurrentUsersProjects()
    {
        AddProject("Mine", _userId);
        AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.GetMyProjectsAsync();

        Assert.Equal(["Mine"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetMyProjectByIdAsync_WhenOwnedByCurrentUser_ReturnsDto()
    {
        var project = AddProject("Mine", _userId);

        var result = await _service.GetMyProjectByIdAsync(project.Id);

        Assert.NotNull(result);
        Assert.Equal(project.Id, result!.Id);
    }

    [Fact]
    public async Task GetMyProjectByIdAsync_WhenOwnedByAnotherUser_ReturnsNull()
    {
        var project = AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.GetMyProjectByIdAsync(project.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProjectAsync_AddsProjectOwnedByCurrentUser()
    {
        var request = new CreateProjectRequest("New Project", "A description");

        var result = await _service.CreateProjectAsync(request);

        Assert.Equal("New Project", result.Name);
        var stored = await _context.Projects.FirstAsync(p => p.Id == result.Id);
        Assert.Equal(_userId, stored.CreatedBy);
    }

    [Fact]
    public async Task CreateProjectAsync_WhenNameAlreadyExistsForCurrentUser_ThrowsBusinessRuleException()
    {
        AddProject("Duplicate", _userId);
        var request = new CreateProjectRequest("Duplicate", null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateProjectAsync(request));
    }

    [Fact]
    public async Task UpdateMyProjectAsync_WhenOwnedByCurrentUser_UpdatesNameAndDescription()
    {
        var project = AddProject("Old Name", _userId);
        var request = new UpdateProjectRequest("New Name", "New description");

        var result = await _service.UpdateMyProjectAsync(project.Id, request);

        Assert.NotNull(result);
        Assert.Equal("New Name", result!.Name);
        Assert.Equal("New description", result.Description);
    }

    [Fact]
    public async Task UpdateMyProjectAsync_WhenOwnedByAnotherUser_ReturnsNull()
    {
        var project = AddProject("Old Name", Guid.NewGuid());
        var request = new UpdateProjectRequest("New Name", null);

        var result = await _service.UpdateMyProjectAsync(project.Id, request);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMyProjectAsync_WhenNameConflictsWithAnotherOwnProject_ThrowsBusinessRuleException()
    {
        AddProject("Taken", _userId);
        var project = AddProject("Original", _userId);
        var request = new UpdateProjectRequest("Taken", null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateMyProjectAsync(project.Id, request));
    }

    [Fact]
    public async Task DeleteMyProjectAsync_WhenOwnedByCurrentUser_SoftDeletesAndReturnsTrue()
    {
        var project = AddProject("Mine", _userId);

        var result = await _service.DeleteMyProjectAsync(project.Id);

        Assert.True(result);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteMyProjectAsync_WhenOwnedByAnotherUser_ReturnsFalse()
    {
        var project = AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.DeleteMyProjectAsync(project.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task GetAllProjectsAsync_ReturnsProjectsFromAllUsers()
    {
        AddProject("Mine", _userId);
        AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.GetAllProjectsAsync();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetAnyProjectByIdAsync_ReturnsProjectRegardlessOfOwner()
    {
        var project = AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.GetAnyProjectByIdAsync(project.Id);

        Assert.NotNull(result);
        Assert.Equal(project.Id, result!.Id);
    }

    [Fact]
    public async Task DeleteAnyProjectAsync_SoftDeletesRegardlessOfOwner()
    {
        var project = AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.DeleteAnyProjectAsync(project.Id);

        Assert.True(result);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteAnyProjectAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteAnyProjectAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task RestoreAnyProjectAsync_WhenDeleted_RestoresAndReturnsDto()
    {
        var project = AddProject("Deleted", _userId, isDeleted: true);

        var result = await _service.RestoreAnyProjectAsync(project.Id);

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    [Fact]
    public async Task RestoreAnyProjectAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.RestoreAnyProjectAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDeletedProjectsAsync_ReturnsOnlyDeletedProjects()
    {
        AddProject("Active", _userId);
        AddProject("Deleted", _userId, isDeleted: true);

        var result = await _service.GetDeletedProjectsAsync();

        Assert.Equal(["Deleted"], result.Select(p => p.Name));
    }
}
