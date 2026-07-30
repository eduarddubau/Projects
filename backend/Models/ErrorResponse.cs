namespace Backend.Models;

public record ErrorResponse
{
    public int StatusCode { get; init; }

    /// <summary>Stable rule identifier for clients that translate; null when the failure
    /// carries no specific rule (validation, 404, unexpected faults).</summary>
    public string? Code { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Correlates this response with its log entry — safe to show to users
    /// and quote in a bug report.</summary>
    public string? TraceId { get; init; }

    public string? Details { get; init; }
}