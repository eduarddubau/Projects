using Backend.Config;
using Backend.DTOs.User;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

// The current user's own account; admin operations on other users live in UsersController.
[Authorize(Policy = AppPolicies.StandardUser)]
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public partial class ProfileController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IUserService userService, ILogger<ProfileController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> GetProfile(CancellationToken ct)
    {
        var profile = await _userService.GetMyProfileAsync(ct);

        if (profile is null)
        {
            LogProfileUserMissing();
            return NotFound(new { message = "Profile not found." });
        }

        return Ok(profile);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> UpdateProfile(
        UpdateProfileRequest request,
        CancellationToken ct
    )
    {
        var profile = await _userService.UpdateMyProfileAsync(request, ct);

        if (profile is null)
        {
            LogProfileUpdateUserMissing();
            return NotFound(new { message = "Profile not found." });
        }

        return Ok(profile);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Profile requested for a user that no longer exists."
    )]
    private partial void LogProfileUserMissing();

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Profile update attempted for a user that no longer exists."
    )]
    private partial void LogProfileUpdateUserMissing();
}
