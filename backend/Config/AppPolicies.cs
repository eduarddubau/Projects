namespace Backend.Config;

public static class AppPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string StandardUser = "StandardUser";

    /// <summary>Rate-limiting policy, not an authorization one. Guards the endpoints
    /// that are actually brute-forced: login and register.</summary>
    public const string AuthThrottle = "AuthThrottle";

    /// <summary>The looser sibling of <see cref="AuthThrottle"/>, for routine session
    /// traffic. Sharing one budget would let token renewals from a shared address
    /// exhaust the login allowance for everyone behind it.</summary>
    public const string SessionThrottle = "SessionThrottle";
}
