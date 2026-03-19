using Backend.Services;
using Backend.DTOs.Auth;
using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend.Config;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AuthController> _logger;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public AuthController(
        UserManager<User> userManager, 
        RoleManager<IdentityRole<Guid>> roleManager,
        ILogger<AuthController> logger, 
        ITokenService tokenService, 
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    [Authorize(Policy = AppPolicies.StandardUser)]
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        return Ok(new
        {
            Message = "You are authorized!",
            UserId = _currentUser.UserId,
            Email = _currentUser.Email,
            FullName = $"{_currentUser.FirstName} {_currentUser.LastName}",
            Roles = _currentUser.Roles
        });
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

   [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = new User 
        { 
            UserName = dto.Email, 
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CreatedBy = "Self-Registration"
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            return BadRequest(ModelState);
        }
        
        const string defaultRole = AppRoles.User;
        if (!await _roleManager.RoleExistsAsync(defaultRole))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(defaultRole));
        }
        await _userManager.AddToRoleAsync(user, defaultRole);

        var token = await _tokenService.CreateToken(user);

        _logger.LogInformation("New user registered and token generated: {Email}", dto.Email);

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