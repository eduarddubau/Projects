using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(AppDbContext context, ILogger<ProductsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        _logger.LogInformation("Retrieving all products.");
        return await _context.Products.Include(p => p.Owner).ToListAsync();
    }

    [HttpGet("{id:Guid}")]
    public async Task<ActionResult<Product>> GetProduct(Guid id)
    {
        _logger.LogInformation("Retrieving product with ID {ProductId}.", id);
        var product = await _context.Products.Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            _logger.LogWarning("Product with ID {ProductId} not found.", id);
            return NotFound(new { message = $"Product with ID {id} not found." });
        }

        _logger.LogInformation("Product with ID {ProductId} retrieved successfully.", id);
        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        _logger.LogInformation("Creating a new product for User ID {UserId}.", product.UserId);
        var userExists = await _context.Users.AnyAsync(u => u.Id == product.UserId);
        
        if (!userExists)
        {
            _logger.LogWarning("User with ID {UserId} not found. Cannot create product.", product.UserId);
            return NotFound(new { message = $"User with ID {product.UserId} not found." });
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Product created successfully with ID {ProductId}.", product.Id);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpDelete("{id:Guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        _logger.LogInformation("Attempting to delete product with ID {ProductId}.", id);
        var product = await _context.Products.FindAsync(id);
        if (product == null) {
            _logger.LogWarning("Product with ID {ProductId} not found.", id);
            return NotFound(new { message = $"Product with ID {id} not found." });
        }

        product.IsDeleted = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Product with ID {ProductId} marked as deleted successfully.", id);
        return NoContent();
    }
}