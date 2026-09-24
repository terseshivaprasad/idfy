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

/// <summary>
/// One IDfy task, in structured columns (no PII): the queryable domain record behind an
/// <see cref="ApiCallLog"/>. Populated from the IDfy request/response envelope.
/// </summary>
public sealed class IdfyTaskLog
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? TraceId { get; set; }
    public string? TaskId { get; set; }
    public string? GroupId { get; set; }
    public string? RequestId { get; set; }
    /// <summary>IDfy task type: document, ind_pan, ind_aadhaar, ind_driving_license, ind_passport.</summary>
    public string? TaskType { get; set; }
    /// <summary>validate or extract.</summary>
    public string? Action { get; set; }
    /// <summary>completed, failed, or null when the call never returned.</summary>
    public string? Status { get; set; }
    public int? HttpStatus { get; set; }
    /// <summary>IDfy error code on failure (e.g. INVALID_IMAGE, INSUFFICIENT_CREDITS).</summary>
    public string? ErrorCode { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset? IdfyCreatedAt { get; set; }
    public DateTimeOffset? IdfyCompletedAt { get; set; }
}

/// <summary>
/// One inbound HTTP call from an internal caller: the request we received and the response we
/// returned (both PII-redacted), and timing. Correlate with <see cref="ApiCallLog"/> via TraceId.
/// </summary>
public sealed class RequestLog
{
    public DateTimeOffset CreatedAt { get; set; }
    public string? TraceId { get; set; }
    public string? ClientIp { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
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
