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

    public AuthController(UserManager<User> userManager, ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _logger = logger;
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

        _logger.LogInformation("User {Email} logged in successfully.", request.Email);

        return Ok(new { 
            Message = "Logged in successfully!",
            User = new { user.Email, user.FirstName, user.LastName }
        });
    }
}