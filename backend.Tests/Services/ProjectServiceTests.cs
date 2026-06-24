using Backend.Config;
using Backend.Data;
using Backend.DTOs.Project;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.Tests.Services;

public class ProjectServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly ProjectService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private const int TrashWindowDays = 30;

    public ProjectServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);
        _service = new ProjectService(_context, _currentUser.Object, Options.Create(new ProjectRetentionOptions { TrashWindowDays = TrashWindowDays }));

        _currentUser.Setup(c => c.UserGuid).Returns(_userId);
    }

    private Project AddProject(string name, Guid createdBy, bool isDeleted = false, DateTime? deletedAt = null)
    {
        var project = new Project { Name = name, CreatedBy = createdBy, IsDeleted = isDeleted, DeletedAt = deletedAt };
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
    public async Task GetMyProjectsAsync_ExcludesDeletedProjects()
    {
        AddProject("Active", _userId);
        AddProject("Deleted", _userId, isDeleted: true);

        var result = await _service.GetMyProjectsAsync();

        Assert.Equal(["Active"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetMyDeletedProjectsAsync_ReturnsOwnDeletedProjectsWithinRetentionWindow()
    {
        AddProject("Active", _userId);
        AddProject("RecentlyDeleted", _userId, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddProject("OldDeleted", _userId, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1)));
        AddProject("Someone else's deleted", Guid.NewGuid(), isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));

        var result = await _service.GetMyDeletedProjectsAsync();

        Assert.Equal(["RecentlyDeleted"], result.Select(p => p.Name));
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
    public async Task DeleteMyProjectByIdAsync_WhenOwnedByCurrentUser_SoftDeletesAndReturnsTrue()
    {
        var project = AddProject("Mine", _userId);

        var result = await _service.DeleteMyProjectByIdAsync(project.Id);

        Assert.True(result);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteMyProjectByIdAsync_WhenOwnedByAnotherUser_ReturnsFalse()
    {
        var project = AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.DeleteMyProjectByIdAsync(project.Id);

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
    public async Task DeleteAnyProjectByIdAsync_SoftDeletesRegardlessOfOwner()
    {
        var project = AddProject("Someone else's", Guid.NewGuid());

        var result = await _service.DeleteAnyProjectByIdAsync(project.Id);

        Assert.True(result);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task DeleteAnyProjectByIdAsync_WhenNotFound_ReturnsFalse()
    {
        var result = await _service.DeleteAnyProjectByIdAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task RestoreAnyProjectsAsync_WhenDeleted_RestoresAndReturnsCount()
    {
        var project = AddProject("Deleted", _userId, isDeleted: true);

        var result = await _service.RestoreAnyProjectsAsync([project.Id]);

        Assert.Equal(1, result);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    [Fact]
    public async Task RestoreAnyProjectsAsync_WhenNotFound_ReturnsZero()
    {
        var result = await _service.RestoreAnyProjectsAsync([Guid.NewGuid()]);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RestoreAnyProjectsAsync_WithMultipleIds_RestoresAllAndReturnsCount()
    {
        var deleted1 = AddProject("Deleted1", _userId, isDeleted: true);
        var deleted2 = AddProject("Deleted2", _userId, isDeleted: true);

        var result = await _service.RestoreAnyProjectsAsync([deleted1.Id, deleted2.Id]);

        Assert.Equal(2, result);
        Assert.False((await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == deleted1.Id)).IsDeleted);
        Assert.False((await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == deleted2.Id)).IsDeleted);
    }

    [Fact]
    public async Task RestoreMyProjectByIdAsync_WhenOwnedByCurrentUserAndDeleted_RestoresAndReturnsDto()
    {
        var project = AddProject("Deleted", _userId, isDeleted: true);

        var result = await _service.RestoreMyProjectByIdAsync(project.Id);

        Assert.NotNull(result);
        Assert.False(result!.IsDeleted);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == project.Id);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedAt);
    }

    [Fact]
    public async Task RestoreMyProjectByIdAsync_WhenOwnedByAnotherUser_ReturnsNull()
    {
        var project = AddProject("Someone else's deleted", Guid.NewGuid(), isDeleted: true);

        var result = await _service.RestoreMyProjectByIdAsync(project.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task RestoreMyProjectByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _service.RestoreMyProjectByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllDeletedProjectsAsync_ReturnsOnlyDeletedProjects()
    {
        AddProject("Active", _userId);
        AddProject("Deleted", _userId, isDeleted: true);

        var result = await _service.GetAllDeletedProjectsAsync();

        Assert.Equal(["Deleted"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task GetAllDeletedProjectsAsync_ReturnsAllDeletedRegardlessOfAge()
    {
        AddProject("RecentlyDeleted", _userId, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddProject("OldDeleted", _userId, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1)));

        var result = await _service.GetAllDeletedProjectsAsync();

        Assert.Equal(["OldDeleted", "RecentlyDeleted"], result.Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task GetAllDeletedProjectsAsync_FlagsOnlyProjectsOlderThanRetentionWindowAsPurgeable()
    {
        AddProject("RecentlyDeleted", _userId, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-1));
        AddProject("OldDeleted", _userId, isDeleted: true, deletedAt: DateTime.UtcNow.AddDays(-(TrashWindowDays + 1)));

        var result = await _service.GetAllDeletedProjectsAsync();

        Assert.False(result.Single(p => p.Name == "RecentlyDeleted").IsPurgeable);
        Assert.True(result.Single(p => p.Name == "OldDeleted").IsPurgeable);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WhenDeleted_HardDeletesAndReturnsCount()
    {
        var project = AddProject("Deleted", _userId, isDeleted: true);

        var result = await _service.PurgeProjectsAsync([project.Id]);

        Assert.Equal(1, result);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == project.Id);
        Assert.Null(stored);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WhenNotDeleted_SkipsItAndDoesNotDelete()
    {
        var project = AddProject("Active", _userId);

        var result = await _service.PurgeProjectsAsync([project.Id]);

        Assert.Equal(0, result);
        var stored = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == project.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WhenNotFound_ReturnsZero()
    {
        var result = await _service.PurgeProjectsAsync([Guid.NewGuid()]);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task PurgeProjectsAsync_WithMultipleIds_PurgesOnlyTheDeletedOnesAndReturnsCount()
    {
        var deleted1 = AddProject("Deleted1", _userId, isDeleted: true);
        var deleted2 = AddProject("Deleted2", _userId, isDeleted: true);
        var active = AddProject("Active", _userId);

        var result = await _service.PurgeProjectsAsync([deleted1.Id, deleted2.Id, active.Id]);

        Assert.Equal(2, result);
        Assert.Null(await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == deleted1.Id));
        Assert.Null(await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == deleted2.Id));
        Assert.NotNull(await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == active.Id));
    }
}
