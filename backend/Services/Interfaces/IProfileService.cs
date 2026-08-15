using Backend.DTOs.User;

namespace Backend.Services.Interfaces;

public interface IProfileService
{
    Task<UserResponseDto?> GetMyProfileAsync(CancellationToken ct = default);
    Task<UserResponseDto?> UpdateMyProfileAsync(
        UpdateProfileRequest dto,
        CancellationToken ct = default
    );
}
