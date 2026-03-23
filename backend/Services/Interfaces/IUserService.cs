using Backend.DTOs.User;

namespace Backend.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserResponseDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<UserResponseDto?> GetAnyUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserResponseDto> CreateUserAsync(CreateUserRequest dto, CancellationToken ct = default);
    Task<bool> DeleteAnyUserAsync(Guid id, CancellationToken ct = default);
    Task<UserResponseDto?> RestoreAnyUserAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<UserResponseDto>> GetDeletedUsersAsync(CancellationToken ct = default);
}