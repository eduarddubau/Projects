using System.ComponentModel.DataAnnotations;

namespace Backend.Config;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    [MinLength(64, ErrorMessage = "JWT Key must be at least 64 characters.")]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 10080)] // From 1 minute to 1 week
    public int DurationInMinutes { get; set; } = 60;
}