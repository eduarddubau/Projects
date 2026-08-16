using Backend.Config;
using Backend.DTOs.Auth;
using Backend.DTOs.User;
using Backend.Exceptions;
using Backend.Mappings;
using Backend.Models;
using Backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
// MVC has a SignInResult too, and it is a different thing entirely.
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(AppPolicies.AuthThrottle)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public partial class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AuthController> _logger;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceService _workspaceService;
    private readonly IInvitationService _invitationService;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<AuthController> logger,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ICurrentUserService currentUser,
        IWorkspaceService workspaceService,
        IInvitationService invitationService
    )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _currentUser = currentUser;
        _workspaceService = workspaceService;
        _invitationService = invitationService;
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> GetCurrentUser(CancellationToken ct)
    {
        if (_currentUser.UserId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(_currentUser.UserId);

        if (user is null)
            return NotFound();

        return Ok(user.MapToDto());
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // CheckPasswordSignInAsync, not UserManager.CheckPasswordAsync: only this one
        // records the failure against the account, which is what makes lockout work.
        var result = user is null
            ? SignInResult.Failed
            : await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true
            );

        if (!result.Succeeded)
        {
            // No email: a failed-login log would otherwise collect addresses of people
            // who never had an account here. Null user means the address is unknown.
            LogLoginFailed(user?.Id, result.IsLockedOut);

            // The same message whether the address is unknown, the password is wrong, or
            // the account is locked — anything more precise is an enumeration oracle.
            return Unauthorized("Invalid credentials.");
        }

        var token = await _tokenService.CreateToken(user!);
        var refreshToken = await _refreshTokenService.IssueAsync(user!.Id, ct);
        LogLoginSucceeded(user.Id);

        return Ok(
            new
            {
                Token = token,
                RefreshToken = refreshToken,
                User = user.MapToDto(),
            }
        );
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct
    )
    {
        var user = request.ToEntity();

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(user, request.Password);
        }
        catch (BusinessRuleException ex) when (ex.Code == BusinessRuleCodes.DuplicateEmail)
        {
            ModelState.AddModelError("DuplicateEmail", ex.Message);
            return BadRequest(ModelState);
        }

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);

            return BadRequest(ModelState);
        }

        await _userManager.AddToRoleAsync(user, AppRoles.User);
        await _workspaceService.EnsurePersonalWorkspaceAsync(user, ct);
        await _invitationService.RedeemPendingForEmailAsync(user, ct);

        var token = await _tokenService.CreateToken(user);
        var refreshToken = await _refreshTokenService.IssueAsync(user.Id, ct);
        LogUserRegistered(user.Id);

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                Token = token,
                RefreshToken = refreshToken,
                User = user.MapToDto(),
            }
        );
    }

    // An action-level policy replaces the controller's.
    [EnableRateLimiting(AppPolicies.SessionThrottle)]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct
    )
    {
        var rotation = await _refreshTokenService.RotateAsync(request.RefreshToken, ct);
        if (!rotation.Succeeded)
            return Unauthorized("Invalid refresh token.");

        var user = await _userManager.FindByIdAsync(rotation.UserId.ToString());
        if (user is null)
            return Unauthorized("Invalid refresh token.");

        var token = await _tokenService.CreateToken(user);

        return Ok(
            new
            {
                Token = token,
                RefreshToken = rotation.NewRawToken,
                User = user.MapToDto(),
            }
        );
    }

    [EnableRateLimiting(AppPolicies.SessionThrottle)]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _refreshTokenService.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed login attempt for user {userId}. Locked out: {lockedOut}."
    )]
    private partial void LogLoginFailed(Guid? userId, bool lockedOut);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {userId} logged in successfully.")]
    private partial void LogLoginSucceeded(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "New user registered: {userId}")]
    private partial void LogUserRegistered(Guid userId);
}
