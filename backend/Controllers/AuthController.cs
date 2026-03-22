using Backend.Services;
using Backend.DTOs.Auth;
using Backend.Models;
using Backend.Mappings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Backend.Config;
using Backend.DTOs;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AuthController> _logger;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(
        UserManager<User> userManager,
        ILogger<AuthController> logger,
        ITokenService tokenService,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _logger = logger;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    [Authorize(Policy = AppPolicies.StandardUser)]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> GetCurrentUser()
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId);

        if (user is null) return NotFound();

        return Ok(user.MapToDto());
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        var passwordValid = user != null && await _userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid)
        {
            _logger.LogWarning("Failed login attempt for: {Email}", request.Email);
            return Unauthorized("Invalid credentials.");
        }

        var token = await _tokenService.CreateToken(user!);
        _logger.LogInformation("User {Email} logged in successfully.", request.Email);

        return Ok(new
        {
            Token = token,
            User = user!.MapToDto()
        });
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = dto.ToEntity();

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);

            return BadRequest(ModelState);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.User); 

        var token = await _tokenService.CreateToken(user);
        _logger.LogInformation("New user registered: {Email}", dto.Email);

        return StatusCode(StatusCodes.Status201Created, new
        {
            Token = token,
            User = user.MapToDto()
        });
    }
}