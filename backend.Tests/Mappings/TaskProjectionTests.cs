using Backend.Data;
using Backend.DTOs.Task;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.Tests.Mappings;

/// <summary>
/// TaskResponseDto is built by three hand-written projections: MapToDto over a loaded
/// entity, MapToDto over an IQueryable, and MapToWorkspaceDto for the derived DTO. EF
/// cannot compose a projection expression into a derived type, so they cannot share a
/// body — these tests are what catches them drifting apart.
///
/// The app-wide "no projection names a soft-deleted person" invariant lives in
/// SoftDeletedNameTests, not here: it covers every mapper, and burying it in a task
/// fixture is how it came to be missing from the sibling files in the first place.
/// </summary>
public sealed class TaskProjectionTests : IDisposable
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly AppDbContext _context;
    private readonly TaskItem _task;

    public TaskProjectionTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options, _currentUser.Object);

        var author = AddUser("Ada", "Lovelace");
        var editor = AddUser("Grace", "Hopper");
        var project = AddProject("Rocket Plans");

        // Every field non-default, including the audit and soft-delete ones: a property
        // left at its default would compare equal across all three and prove nothing.
        _task = new TaskItem
        {
            Title = "Rebuild the homepage",
            Description = "Every field populated on purpose.",
            Status = TaskItemStatus.Done,
            Position = 3,
            ProjectId = project.Id,
            AssigneeId = editor.Id,
            StartDate = new DateOnly(2026, 8, 1),
            DueDate = new DateOnly(2026, 8, 20),
            CompletedAt = new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            CreatedBy = author.Id,
            UpdatedAt = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc),
            UpdatedBy = editor.Id,
            IsDeleted = true,
            DeletedAt = new DateTime(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc),
        };

        _context.Tasks.Add(_task);
        // Synchronous: SaveChangesAsync carries the audit interceptor, which would overwrite
        // the very fields this fixture is pinning.
        _context.SaveChanges();
    }

    private User AddUser(string first, string last)
    {
        var user = new User
        {
            FirstName = first,
            LastName = last,
            Email = $"{first}@example.com".ToLowerInvariant(),
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private Project AddProject(string name)
    {
        var workspace = new Workspace { Name = "Acme", IsPersonal = false };
        _context.Workspaces.Add(workspace);
        _context.SaveChanges();

        var project = new Project { Name = name, WorkspaceId = workspace.Id };
        _context.Projects.Add(project);
        _context.SaveChanges();
        return project;
    }

    private TaskResponseDto FromEntity() =>
        _context
            .Tasks.IgnoreQueryFilters()
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .Include(t => t.Updater)
            .Single(t => t.Id == _task.Id)
            .MapToDto();

    private TaskResponseDto FromQuery() =>
        _context.Tasks.IgnoreQueryFilters().Where(t => t.Id == _task.Id).MapToDto().Single();

    private WorkspaceTaskResponseDto FromWorkspaceQuery() =>
        _context
            .Tasks.IgnoreQueryFilters()
            .Where(t => t.Id == _task.Id)
            .MapToWorkspaceDto()
            .Single();

    // Guards the test itself: if the fixture stopped populating a field, every projection
    // would return the same default and the comparison below would pass while proving nothing.
    [Fact]
    public void TheFixturePopulatesEveryFieldOfTheDto()
    {
        var dto = FromEntity();

        var unset = typeof(TaskResponseDto)
            .GetProperties()
            .Where(p =>
            {
                var value = p.GetValue(dto);
                var fallback = p.PropertyType.IsValueType
                    ? Activator.CreateInstance(p.PropertyType)
                    : null;
                return value is null || value.Equals(fallback) || (value as string) == string.Empty;
            })
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(unset);
    }

    [Fact]
    public void TheQueryProjectionMatchesTheEntityOne()
    {
        AssertSameDto(FromEntity(), FromQuery());
    }

    [Fact]
    public void TheWorkspaceProjectionMatchesTheEntityOneAndAddsTheProjectName()
    {
        var workspaceDto = FromWorkspaceQuery();

        AssertSameDto(FromEntity(), workspaceDto);
        Assert.Equal("Rocket Plans", workspaceDto.ProjectName);
    }

    private static void AssertSameDto(TaskResponseDto expected, TaskResponseDto actual)
    {
        foreach (var property in typeof(TaskResponseDto).GetProperties())
        {
            // Named, or a drift failure reads as two anonymous values and someone has to
            // bisect eighteen properties to find which projection forgot one.
            Assert.True(
                Equals(property.GetValue(expected), property.GetValue(actual)),
                $"{property.Name}: expected {property.GetValue(expected)}, "
                    + $"got {property.GetValue(actual)}"
            );
        }
    }

    public void Dispose() => _context.Dispose();
}
