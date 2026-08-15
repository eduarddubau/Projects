namespace Backend.Config;

/// <summary>Both halves of the brute-force defence: a per-IP throttle on the auth
/// endpoints, and per-account lockout for an attacker who rotates addresses.</summary>
public class AuthProtectionOptions
{
    public const string SectionName = "AuthProtection";

    /// <summary>Requests one client IP may make to /api/auth within the window.</summary>
    public int PermitPerWindow { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    /// <summary>Failed passwords before the account itself locks, per OWASP's account-level lockout.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    /// <summary>
    /// Proxy networks in CIDR form whose X-Forwarded-For may be believed. Empty means
    /// trust nothing and read the socket address, which is correct for a directly
    /// exposed API and wrong behind a proxy — see ForwardedHeaders in ServiceExtensions.
    /// </summary>
    public string[] TrustedProxyNetworks { get; set; } = [];
}
