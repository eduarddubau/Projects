using Backend.Models;

namespace Backend.Services.Interfaces;

public interface ITokenService
{
    Task<string> CreateToken(User user);
}