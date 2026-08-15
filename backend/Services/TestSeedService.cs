using Backend.Data;
using Backend.DTOs.TestSeed;
using Backend.Models;
using Backend.Services.Interfaces;

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

    public TestSeedService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SeededProjectDto>> SeedDeletedProjectsAsync(
        SeedDeletedProjectsRequest request,
        CancellationToken ct = default
    )
    {
        var items = request.Projects;

        var projects = items
            .Select(p => new Project
            {
                Name = p.Name,
                Description = "Seeded by an E2E test.",
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
