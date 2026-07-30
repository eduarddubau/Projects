using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Backend.Middleware;

/// <summary>Logs request outcomes that never reach <see cref="GlobalExceptionHandler"/> —
/// auth 401s, FluentValidation 400s and controller <c>return NotFound()</c> are status
/// codes rather than exceptions, so nothing else sees them.
/// <para>Failures log at Warning/Error. Successes log at Debug, which the default
/// Information minimum discards — raise Serilog:MinimumLevel to Debug to get full
/// request logging without a rebuild.</para></summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.GetTimestamp();

        await _next(context);

        var statusCode = context.Response.StatusCode;

        var level = statusCode switch
        {
            >= StatusCodes.Status500InternalServerError => LogLevel.Error,
            >= StatusCodes.Status400BadRequest          => LogLevel.Warning,
            _                                          => LogLevel.Debug,
        };

        if (!_logger.IsEnabled(level)) return;

        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // Populated by the authentication middleware, which runs inside this one.
        var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        _logger.Log(level,
            "{Method} {Path} responded {StatusCode} for user {UserId} in {ElapsedMs:0.0}ms. TraceId {TraceId}",
            context.Request.Method, context.Request.Path, statusCode,
            userId, elapsedMs, traceId);
    }
}
