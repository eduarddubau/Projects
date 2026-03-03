using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(AppDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        _logger.LogInformation("Retrieving all users.");
        return await _context.Users.ToListAsync();
    }

    [HttpGet("{id:Guid}")]
    public async Task<ActionResult<User>> GetUser(Guid id)
    {
        _logger.LogInformation("Retrieving user with ID {UserId}.", id);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found.", id);
            return NotFound(new { message = $"User with ID {id} not found." });
        }

        _logger.LogInformation("User with ID {UserId} retrieved successfully.", id);
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(User user)
    {   
        _logger.LogInformation("Creating a new user with email {Email}.", user.Email);
        bool emailExists = await _context.Users.AnyAsync(u => u.Email == user.Email);
    
        if (emailExists)
        {
            _logger.LogWarning("Email {Email} is already registered. Cannot create user.", user.Email);
            return Conflict(new { message = "This email is already registered." });
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User created successfully with ID {UserId}.", user.Id);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpDelete("{id:Guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        _logger.LogInformation("Attempting to delete user with ID {UserId}.", id);
        var user = await _context.Users.FindAsync(id);

        if (user == null) {
            _logger.LogWarning("User with ID {UserId} not found.", id);
            return NotFound(new { message = $"User with ID {id} not found." });
        }

        user.IsDeleted = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User with ID {UserId} marked as deleted successfully.", id);
        return NoContent();
    }

}