using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using Backend.Config;
using Backend.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Backend.Middleware;

/// <summary>
/// Shapes a throttled request into the same ErrorResponse every other failure returns,
/// and logs it — a caller hitting the limit is a security signal, so it belongs in the
/// log beside the failed logins it is usually made of.
/// </summary>
public static partial class RateLimitRejectionHandler
{
    public static async ValueTask HandleAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken
    )
    {
        var httpContext = context.HttpContext;

        // A sliding window supplies no RetryAfter on the immediate-rejection path
        // (QueueLimit 0) despite advertising the metadata, so the window is the fallback.
        // Erring long is deliberate: too short and the client retries into another 429.
        var retryAfterSeconds = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var metadataRetryAfter
        )
            ? (int)Math.Ceiling(metadataRetryAfter.TotalSeconds)
            : httpContext
                .RequestServices.GetRequiredService<IOptions<AuthProtectionOptions>>()
                .Value.WindowSeconds;

        var retryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        httpContext.Response.Headers.RetryAfter = retryAfter;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var logger = httpContext
            .RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RateLimitRejectionHandler));

        LogRequestThrottled(
            logger,
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            retryAfterSeconds,
            traceId
        );

        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
                Code = BusinessRuleCodes.TooManyRequests,
                Message = "Too many requests. Please try again shortly.",
                Params = new Dictionary<string, string> { ["retryAfterSeconds"] = retryAfter },
                TraceId = traceId,
            },
            cancellationToken
        );
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Request throttled on {method} {path} from {clientIp}, retry after {retryAfterSeconds}s. TraceId {traceId}"
    )]
    private static partial void LogRequestThrottled(
        ILogger logger,
        string method,
        string path,
        string? clientIp,
        int retryAfterSeconds,
        string traceId
    );
}
