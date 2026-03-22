using Backend.DTOs;

namespace Backend.Services;

public interface IUserService
{
    Task<IEnumerable<UserResponseDto>> GetAllUsersAsync();
    Task<UserResponseDto?> GetAnyUserByIdAsync(Guid id);
    Task<UserResponseDto> CreateUserAsync(CreateUserDto dto);
    Task<bool> DeleteAnyUserAsync(Guid id);
    Task<UserResponseDto?> RestoreAnyUserAsync(Guid id);
    Task<IEnumerable<UserResponseDto>> GetDeletedUsersAsync();

}