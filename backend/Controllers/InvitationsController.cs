using Backend.Config;
using Backend.DTOs.Workspace;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// Controller boundaries follow service boundaries; route shape follows resource nesting.
// The absolute routes below are what lets those two be independent — the URLs stay nested
// under the workspace they belong to while every invitation endpoint lives in one file.
[Authorize(Policy = AppPolicies.StandardUser)]
[ApiController]
[Route("api/invitations")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/invitations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<InvitationResponseDto>>> GetPending(
        Guid workspaceId, CancellationToken ct)
        => Ok(await _invitationService.GetPendingAsync(workspaceId, ct));

    // Ok, not CreatedAtAction: the two outcomes create different resources (a membership row
    // or an invitation row), so there is no single Location to point at. InviteOutcome in the
    // body is what tells the client which happened.
    [HttpPost("/api/workspaces/{workspaceId:guid}/invitations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InviteResultDto>> Invite(
        Guid workspaceId, [FromBody] InviteRequest dto, CancellationToken ct)
        => Ok(await _invitationService.InviteAsync(workspaceId, dto, ct));

    [HttpDelete("/api/workspaces/{workspaceId:guid}/invitations/{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(Guid workspaceId, Guid invitationId, CancellationToken ct)
    {
        await _invitationService.RevokeAsync(workspaceId, invitationId, ct);
        return NoContent();
    }

    // No workspace in scope: the token is the only thing identifying anything. Still
    // [Authorize] though — bearer semantics mean we don't care *which* account redeems,
    // but there has to be one to attach the membership to.
    [HttpPost("accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkspaceResponseDto>> Accept(
        [FromBody] AcceptInviteRequest dto, CancellationToken ct)
        => Ok(await _invitationService.AcceptAsync(dto.Token, ct));
}
