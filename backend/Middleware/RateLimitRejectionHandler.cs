using System.Diagnostics;
using System.Globalization;
using System.Threading.RateLimiting;
using Backend.Config;
using Backend.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Middleware;

/// <summary>
/// Shapes a throttled request into the same ErrorResponse every other failure returns,
/// and logs it as a security event.
/// </summary>
public static partial class RateLimitRejectionHandler
{
    /// <summary>
    /// <paramref name="fallbackWindowSeconds"/> is the Retry-After used when the limiter
    /// offers none, so every policy sharing this handler must share that window.
    /// </summary>
    public static Func<OnRejectedContext, CancellationToken, ValueTask> WithFallbackWindow(
        int fallbackWindowSeconds
    ) =>
        (context, cancellationToken) =>
            HandleAsync(context, fallbackWindowSeconds, cancellationToken);

    private static async ValueTask HandleAsync(
        OnRejectedContext context,
        int fallbackWindowSeconds,
        CancellationToken cancellationToken
    )
    {
        var httpContext = context.HttpContext;

        // A sliding window supplies no RetryAfter on the immediate-rejection path
        // (QueueLimit 0) despite advertising the metadata, so the fallback is load-bearing
        // rather than defensive.
        var retryAfterSeconds = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var metadataRetryAfter
        )
            ? (int)Math.Ceiling(metadataRetryAfter.TotalSeconds)
            : fallbackWindowSeconds;

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
