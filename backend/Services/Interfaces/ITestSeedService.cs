using Backend.DTOs.TestSeed;

namespace Backend.Services.Interfaces;

public interface ITestSeedService
{
    Task<IReadOnlyList<SeededProjectDto>> SeedDeletedProjectsAsync(
        SeedDeletedProjectsRequest request,
        CancellationToken ct = default
    );
}
