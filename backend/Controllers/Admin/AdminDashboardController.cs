using Backend.Config;
using Backend.DTOs.Dashboard;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Admin;

[Authorize(Policy = AppPolicies.AdminOnly)]
[ApiController]
[Route("api/admin/dashboard")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AdminDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardDto>> GetAdminDashboard(CancellationToken ct)
    {
        var dashboard = await _dashboardService.GetAdminDashboardAsync(ct);
        return Ok(dashboard);
    }
}
