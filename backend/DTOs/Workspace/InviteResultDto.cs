namespace Backend.DTOs.Workspace;

public enum InviteOutcome
{
    Joined,
    Invited,
}

public record InviteResultDto(
    InviteOutcome Outcome,
    string? Token,
    WorkspaceMemberResponseDto? Member
);
