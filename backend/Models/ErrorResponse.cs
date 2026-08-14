namespace Backend.Models;

public record ErrorResponse
{
    public int StatusCode { get; init; }

    public string? Code { get; init; }

    public IReadOnlyDictionary<string, string>? Params { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? TraceId { get; init; }

    public string? Details { get; init; }
}
