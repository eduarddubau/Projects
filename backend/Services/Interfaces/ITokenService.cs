using Backend.Models;

namespace Backend.Services;

public interface ITokenService
{
    Task<string> CreateToken(User user);
}