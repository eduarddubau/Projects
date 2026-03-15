using Backend.Models;

namespace Backend.Services;

public interface IUserService
{
    Task<IEnumerable<User>> GetUsersAsync();
    Task<User?> GetUserByIdAsync(Guid id);
    Task<User> CreateUserAsync(User user);
    Task<bool> DeleteUserAsync(Guid id);
}