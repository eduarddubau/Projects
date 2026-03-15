using Backend.Config;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
    {
        _logger.LogInformation("Retrieving all users.");
        var users = await _userService.GetUsersAsync();
        return Ok(users);
    }

    [HttpGet("{id:Guid}")]
    public async Task<ActionResult<UserResponseDto>> GetUser(Guid id)
    {
        _logger.LogInformation("Retrieving user with ID {UserId}.", id);
        var user = await _userService.GetUserByIdAsync(id);

        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found.", id);
            return NotFound(new { message = $"User with ID {id} not found." });
        }

        _logger.LogInformation("User with ID {UserId} retrieved successfully.", id);
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> CreateUser(CreateUserDto createUserDto)
    {
        _logger.LogInformation("Creating a new user with email {Email}.", createUserDto.Email);

        try
        {
            var createdUser = await _userService.CreateUserAsync(createUserDto);
            _logger.LogInformation("User created successfully with ID {UserId}.", createdUser.Id);
            return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, createdUser);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Email {Email} is already registered. Cannot create user.", createUserDto.Email);
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:Guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        _logger.LogInformation("Attempting to delete user with ID {UserId}.", id);

        var result = await _userService.DeleteUserAsync(id);

        if (!result)
        {
            _logger.LogWarning("User with ID {UserId} not found.", id);
            return NotFound(new { message = $"User with ID {id} not found." });
        }

        _logger.LogInformation("User with ID {UserId} marked as deleted successfully.", id);
        return NoContent();
    }

}