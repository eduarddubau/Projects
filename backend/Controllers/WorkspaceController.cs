using Backend.Config;
using Backend.DTOs.Workspace;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[Authorize(Policy = AppPolicies.StandardUser)]
[ApiController]
[Route("api/workspaces")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkspaceResponseDto>>> GetMyWorkspaces(
        CancellationToken ct
    )
    {
        return Ok(await _workspaceService.GetMyWorkspacesAsync(ct));
    }

    [HttpGet("trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkspaceResponseDto>>> GetMyDeletedWorkspaces(
        CancellationToken ct
    )
    {
        return Ok(await _workspaceService.GetDeletedWorkspacesAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceResponseDto>> GetWorkspaceById(
        Guid id,
        CancellationToken ct
    )
    {
        var workspace = await _workspaceService.GetWorkspaceByIdAsync(id, ct);

        if (workspace is null)
            return NotFound();

        return Ok(workspace);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkspaceResponseDto>> CreateWorkspace(
        [FromBody] CreateWorkspaceRequest dto,
        CancellationToken ct
    )
    {
        var workspace = await _workspaceService.CreateWorkspaceAsync(dto, ct);
        return CreatedAtAction(nameof(GetWorkspaceById), new { id = workspace.Id }, workspace);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceResponseDto>> UpdateWorkspace(
        Guid id,
        [FromBody] UpdateWorkspaceRequest dto,
        CancellationToken ct
    )
    {
        return Ok(await _workspaceService.UpdateWorkspaceAsync(id, dto, ct));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteWorkspace(Guid id, CancellationToken ct)
    {
        await _workspaceService.DeleteWorkspaceAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceResponseDto>> RestoreWorkspace(
        Guid id,
        CancellationToken ct
    )
    {
        return Ok(await _workspaceService.RestoreWorkspaceAsync(id, ct));
    }

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<WorkspaceMemberResponseDto>>> GetMembers(
        Guid id,
        CancellationToken ct
    )
    {
        return Ok(await _workspaceService.GetMembersAsync(id, ct));
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkspaceMemberResponseDto>> AddMember(
        Guid id,
        [FromBody] AddMemberRequest dto,
        CancellationToken ct
    )
    {
        var member = await _workspaceService.AddMemberAsync(id, dto, ct);
        return CreatedAtAction(nameof(GetMembers), new { id }, member);
    }

    [HttpPatch("{id:guid}/members/{userId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkspaceMemberResponseDto>> ChangeMemberRole(
        Guid id,
        Guid userId,
        [FromBody] ChangeMemberRoleRequest dto,
        CancellationToken ct
    )
    {
        // Non-null by ChangeMemberRoleRequestValidator.
        return Ok(await _workspaceService.ChangeRoleAsync(id, userId, dto.Role!.Value, ct));
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await _workspaceService.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/members/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await _workspaceService.LeaveAsync(id, ct);
        return NoContent();
    }
}
