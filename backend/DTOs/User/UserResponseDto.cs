using Backend.DTOs;

namespace Backend.DTOs.User;

public record UserResponseDto : AuditResponseDto
{
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
}
