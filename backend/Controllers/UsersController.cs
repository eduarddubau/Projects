using Backend.DTOs.User;
using Backend.Exceptions;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _userService.GetAllUsersAsync(ct);
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> GetUser(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetAnyUserByIdAsync(id, ct);

        if (user is null)
        {
            _logger.LogWarning("User with ID {UserId} not found.", id);
            return NotFound(new { message = $"User with ID {id} not found." });
        }

        return Ok(user);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponseDto>> CreateUser(CreateUserDto createUserDto, CancellationToken ct)
    {
        var createdUser = await _userService.CreateUserAsync(createUserDto, ct);
        return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, createdUser);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var result = await _userService.DeleteAnyUserAsync(id, ct);

        if (!result)
        {
            _logger.LogWarning("User with ID {UserId} not found for deletion.", id);
            return NotFound(new { message = $"User with ID {id} not found." });
        }

        return NoContent();
    }

    [HttpPatch("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> RestoreUser(Guid id, CancellationToken ct)
    {
        var restoredUser = await _userService.RestoreAnyUserAsync(id, ct);

        if (restoredUser is null)
            return NotFound(new { message = $"User with ID {id} not found." });

        return Ok(restoredUser);
    }

    [HttpGet("trash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetDeletedUsers(CancellationToken ct)
    {
        var trash = await _userService.GetDeletedUsersAsync(ct);
        return Ok(trash);
    }
}