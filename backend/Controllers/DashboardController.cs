using Backend.Config;
using Backend.DTOs.Dashboard;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// At-a-glance summaries; per-project rollups would live under Projects.
[Authorize(Policy = AppPolicies.StandardUser)]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDashboardDto>> GetMyDashboard(CancellationToken ct)
    {
        var dashboard = await _dashboardService.GetMyDashboardAsync(ct);
        return Ok(dashboard);
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet("admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminDashboard(CancellationToken ct)
    {
        var dashboard = await _dashboardService.GetAdminDashboardAsync(ct);
        return Ok(dashboard);
    }
}
