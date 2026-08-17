using Backend.Config;
using Backend.DTOs.Dashboard;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// At-a-glance summaries; per-project rollups would live under Projects. The action's
// route is absolute so the summary nests under the workspace that scopes it, the way
// TasksController.GetProjectTasks nests a list under its project. The class-level
// [Route] stays for any later action that doesn't do that — without it, a relative
// route here would land at the site root rather than under /api.
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

    [HttpGet("/api/workspaces/{workspaceId:guid}/dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceDashboardDto>> GetWorkspaceDashboard(
        Guid workspaceId,
        CancellationToken ct
    )
    {
        var dashboard = await _dashboardService.GetWorkspaceDashboardAsync(workspaceId, ct);
        return dashboard is null ? NotFound() : Ok(dashboard);
    }
}
