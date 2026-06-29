using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.DTOs.TestSeed;
using Backend.Services.Interfaces;

namespace Backend.Controllers;

/// <summary>
/// Test-only fixtures so E2E specs can seed their own data and stay idempotent
/// across repeated runs. Every endpoint is gated to the Development environment
/// and returns 404 otherwise, so it never exists in a deployed app.
/// </summary>
[Authorize(Policy = AppPolicies.AdminOnly)]
[ApiController]
[Route("api/test-seed")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class TestSeedController : ControllerBase
{
    private readonly ITestSeedService _testSeedService;
    private readonly IWebHostEnvironment _env;

    public TestSeedController(ITestSeedService testSeedService, IWebHostEnvironment env)
    {
        _testSeedService = testSeedService;
        _env = env;
    }

    /// <summary>
    /// Creates soft-deleted projects owned by the caller with backdated DeletedAt
    /// timestamps, so admin trash/purge specs can exercise age filters without
    /// relying on (and consuming) shared seed data.
    /// </summary>
    [HttpPost("deleted-projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedDeletedProjects(SeedDeletedProjectsRequest request, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var seeded = await _testSeedService.SeedDeletedProjectsAsync(request, ct);
        return Ok(seeded);
    }
}
