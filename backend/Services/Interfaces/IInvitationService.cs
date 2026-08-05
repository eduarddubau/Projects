using Backend.DTOs.Workspace;
using Backend.Models;

namespace Backend.Services.Interfaces;

public interface IInvitationService
{
    Task<InviteResultDto> InviteAsync(Guid workspaceId, InviteRequest dto, CancellationToken ct = default);
    Task<IEnumerable<InvitationResponseDto>> GetPendingAsync(Guid workspaceId, CancellationToken ct = default);
    Task RevokeAsync(Guid workspaceId, Guid invitationId, CancellationToken ct = default);
    Task<WorkspaceResponseDto> AcceptAsync(string rawToken, CancellationToken ct = default);
    Task RedeemPendingForEmailAsync(User user, CancellationToken ct = default);
}
