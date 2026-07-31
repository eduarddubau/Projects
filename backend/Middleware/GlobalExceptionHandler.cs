using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Backend.Models;
using Backend.Exceptions;

namespace Backend.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, message, code, parameters) = exception switch
        {
            BusinessRuleException ex    => (StatusCodes.Status409Conflict, ex.Message, ex.Code, ex.Params),
            NotFoundException           => (StatusCodes.Status404NotFound, exception.Message, null, null),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, exception.Message, null, null),
            _                           => (StatusCodes.Status500InternalServerError, "A critical error occurred on the server.", null, null)
        };

        // Returned to the caller as well, so a reported error can be found in the logs.
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        // Id, never email — logs outlive accounts and shouldn't hold personal data.
        var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Mapped 4xx are expected traffic, not faults — logging them as errors
        // buries real ones and lets anyone probing ids fill the log.
        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception,
                "Unhandled exception ({StatusCode}) on {Method} {Path} for user {UserId}. TraceId {TraceId}",
                statusCode, httpContext.Request.Method, httpContext.Request.Path, userId, traceId);
        else
            _logger.LogWarning(
                "Request rejected ({StatusCode}, {Code}) on {Method} {Path} for user {UserId}: {Message} TraceId {TraceId}",
                statusCode, code, httpContext.Request.Method, httpContext.Request.Path, userId, exception.Message, traceId);

        var response = new ErrorResponse
        {
            StatusCode = statusCode,
            Code = code,
            Params = parameters,
            Message = message,
            TraceId = traceId,
            Details = _env.IsDevelopment() ? exception.ToString() : null
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
