using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

public class Product : BaseEntity
{
    [Required(ErrorMessage = "Product name is required")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 10000.00, ErrorMessage = "Price must be between 0.01 and 10,000")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required]
    public Guid UserId { get; set; }

    // Navigation property
    public User? Owner { get; set; }
}