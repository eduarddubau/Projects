using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Backend.Middleware;

/// <summary>Logs failed responses that never reach <see cref="GlobalExceptionHandler"/> —
/// auth 401s, FluentValidation 400s and controller <c>return NotFound()</c>. Those are
/// status codes rather than exceptions, so nothing else sees them.</summary>
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

        // Deliberately no try/catch: an exception unwinds past this point to the handler
        // registered outside us, which logs it. Skipping our own log avoids duplicates.
        await _next(context);

        if (context.Response.StatusCode < StatusCodes.Status400BadRequest) return;

        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // Populated by the authentication middleware, which runs inside this one.
        var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var level = context.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogLevel.Error
            : LogLevel.Warning;

        _logger.Log(level,
            "Request failed ({StatusCode}) on {Method} {Path} for user {UserId} in {ElapsedMs:0.0}ms. TraceId {TraceId}",
            context.Response.StatusCode, context.Request.Method, context.Request.Path,
            userId, elapsedMs, traceId);
    }
}
