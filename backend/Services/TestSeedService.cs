using Backend.Data;
using Backend.DTOs.TestSeed;
using Backend.Exceptions;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Creates ad-hoc test fixtures for E2E specs. This is distinct from
/// <see cref="DbSeeder"/>, which seeds baseline data once at startup: this runs
/// on demand over HTTP and is only reachable in the Development environment
/// (enforced by <c>TestSeedController</c>).
/// </summary>
public class TestSeedService : ITestSeedService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TestSeedService(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SeededProjectDto>> SeedDeletedProjectsAsync(
        SeedDeletedProjectsRequest request,
        CancellationToken ct = default
    )
    {
        var items = request.Projects;

        // (Guid?), not Guid: a projected value type comes back as Guid.Empty when there
        // is no row, and Guid.Empty would fail the FK rather than say what went wrong.
        var workspaceId =
            await _context
                .Workspaces.Where(w =>
                    w.IsPersonal && w.Members.Any(m => m.UserId == _currentUser.UserGuid)
                )
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("The caller has no personal workspace to seed into.");

        var projects = items
            .Select(p => new Project
            {
                Name = p.Name,
                Description = "Seeded by an E2E test.",
                WorkspaceId = workspaceId,
                // CreatedBy is set to the current user by the audit hook in SaveChanges.
            })
            .ToList();

        // Mirror the seeder: rows are added active first (SaveChanges forces
        // IsDeleted = false), then soft-deleted with a backdated timestamp. The
        // second save is a Modify, so the audit hook leaves our DeletedAt intact.
        _context.Projects.AddRange(projects);
        await _context.SaveChangesAsync(ct);

        for (var i = 0; i < projects.Count; i++)
        {
            projects[i].IsDeleted = true;
            projects[i].DeletedAt = DateTime.UtcNow.AddDays(-items[i].DeletedDaysAgo);
        }
        await _context.SaveChangesAsync(ct);

        return projects.Select(p => new SeededProjectDto { Id = p.Id, Name = p.Name }).ToList();
    }
}
