using Backend.DTOs;

namespace Backend.Services;

public interface IUserService
{
    Task<IEnumerable<UserResponseDto>> GetUsersAsync();
    Task<UserResponseDto?> GetUserByIdAsync(Guid id);
    Task<UserResponseDto> CreateUserAsync(CreateUserDto dto);
    Task<bool> DeleteUserAsync(Guid id);
}