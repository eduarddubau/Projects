using Backend.DTOs.User;

namespace Backend.Services.Admin.Interfaces;

public interface IAdminUserService
{
    Task<IEnumerable<UserResponseDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<UserResponseDto?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserResponseDto> CreateUserAsync(CreateUserRequest dto, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default);
    Task<UserResponseDto?> RestoreUserAsync(Guid id, CancellationToken ct = default);
    Task<bool> AnonymizeUserAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<UserResponseDto>> GetDeletedUsersAsync(CancellationToken ct = default);
}
