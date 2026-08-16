using Backend.Config;
using Microsoft.Extensions.Options;

namespace Backend.Extensions;

public static partial class ForwardedHeaderExtensions
{
    public static void WarnIfNoTrustedProxies(this WebApplication app)
    {
        var trusted = app
            .Services.GetRequiredService<IOptions<AuthProtectionOptions>>()
            .Value.TrustedProxyNetworks;

        if (trusted.Length == 0)
            LogNoTrustedProxies(app.Logger);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AuthProtection:TrustedProxyNetworks is empty: forwarded headers are ignored and the socket address is the rate-limit key. Correct only if this API is exposed directly."
    )]
    private static partial void LogNoTrustedProxies(ILogger logger);
}
