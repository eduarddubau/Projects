namespace Backend.Config;

public static class AppPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string StandardUser = "StandardUser";

    /// <summary>Rate-limiting policy, not an authorization one.</summary>
    public const string AuthThrottle = "AuthThrottle";
}
