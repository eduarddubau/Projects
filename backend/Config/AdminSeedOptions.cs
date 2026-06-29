namespace Backend.Config;

/// <summary>
/// Credentials for the single admin account seeded outside Development. Supply
/// these via configuration/secrets (e.g. AdminSeed__Email / AdminSeed__Password
/// environment variables); when unset, no admin is seeded.
/// </summary>
public class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string FirstName { get; set; } = "Admin";
    public string LastName { get; set; } = "User";
}
