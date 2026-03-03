using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class User : BaseEntity
{  
    [Required(ErrorMessage = "Username is mandatory")]
    [StringLength(50, MinimumLength = 3)]
    public required string Username { get; set; }

    [Required(ErrorMessage = "Email is mandatory")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public required string Email { get; set; }
}