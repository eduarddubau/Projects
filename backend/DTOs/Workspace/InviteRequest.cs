using Backend.Models;

namespace Backend.DTOs.Workspace;

public record InviteRequest(string Email, WorkspaceRole Role);
