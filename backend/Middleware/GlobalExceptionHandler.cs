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
        var (statusCode, message) = exception switch
        {
            BusinessRuleException       => (StatusCodes.Status409Conflict, exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, exception.Message),
            _                           => (StatusCodes.Status500InternalServerError, "A critical error occurred on the server.")
        };

        _logger.LogError(exception, "Unhandled exception ({StatusCode}): {Message}", statusCode, exception.Message);

        var response = new ErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            Details = _env.IsDevelopment() ? exception.ToString() : null
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}