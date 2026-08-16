namespace Backend.Config;

/// <summary>Both halves of the brute-force defence: a per-IP throttle on the auth
/// endpoints, and per-account lockout for an attacker who rotates addresses.</summary>
public class AuthProtectionOptions
{
    public const string SectionName = "AuthProtection";

    /// <summary>Requests one client IP may make to login and register within the window.</summary>
    public int PermitPerWindow { get; set; } = 10;

    /// <summary>The same, for refresh and logout — see <c>AppPolicies.SessionThrottle</c>
    /// for why it is looser.</summary>
    public int SessionPermitPerWindow { get; set; } = 60;

    public int WindowSeconds { get; set; } = 60;

    /// <summary>Failed passwords before the account itself locks, per OWASP's account-level lockout.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    /// <summary>
    /// Proxy networks in CIDR form whose X-Forwarded-For may be believed. Empty switches
    /// forwarded headers off entirely and reads the socket address — correct for a
    /// directly exposed API, and behind a proxy it throttles everyone as one caller
    /// rather than trusting a header anyone can forge.
    /// </summary>
    public string[] TrustedProxyNetworks { get; set; } = [];
}
