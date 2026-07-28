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
            NotFoundException           => (StatusCodes.Status404NotFound, exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, exception.Message),
            _                           => (StatusCodes.Status500InternalServerError, "A critical error occurred on the server.")
        };

        // Mapped 4xx are expected traffic, not faults — logging them as errors
        // buries real ones and lets anyone probing ids fill the log.
        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception ({StatusCode}): {Message}", statusCode, exception.Message);
        else
            _logger.LogWarning("Request rejected ({StatusCode}): {Message}", statusCode, exception.Message);

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