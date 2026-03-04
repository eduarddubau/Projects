using Backend.Services;
using Backend.DTOs.Auth;
using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AuthController> _logger;
    private readonly ITokenService _tokenService;

    public AuthController(UserManager<User> userManager, ILogger<AuthController> logger, ITokenService tokenService)
    {
        _userManager = userManager;
        _logger = logger;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for user: {Email}", request.Email);

        var user = await _userManager.FindByEmailAsync(request.Email);
        
        if (user == null)
        {
            _logger.LogWarning("Login failed: User with email {Email} not found.", request.Email);
            return Unauthorized("Invalid credentials.");
        }

        var result = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!result)
        {
            _logger.LogWarning("Login failed: Incorrect password for user {Email}.", request.Email);
            return Unauthorized("Invalid credentials.");
        }

        var token = await _tokenService.CreateToken(user);

        _logger.LogInformation("Token generated for {Email}", request.Email);

        return Ok(new 
        { 
            Token = token,
            User = new 
            { 
                Id = user.Id,
                Email = user.Email, 
                FirstName = user.FirstName, 
                LastName = user.LastName 
            }
        });
    }
}