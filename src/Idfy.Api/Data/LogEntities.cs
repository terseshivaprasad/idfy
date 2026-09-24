namespace Idfy.Api.Data;

/// <summary>One outbound call to IDfy: request, response (or exception) and timing.</summary>
public sealed class ApiCallLog
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? TraceId { get; set; }
    public string? TaskId { get; set; }
    public string? GroupId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? RequestBody { get; set; }
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
    public string? Exception { get; set; }
}

/// <summary>An unhandled exception raised while processing an inbound request.</summary>
public sealed class ErrorLog
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? TraceId { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
