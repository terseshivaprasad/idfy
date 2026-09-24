using System.Diagnostics;
using Idfy.Api.Data;

namespace Idfy.Api.Logging;

/// <summary>
/// Records every inbound API call (request, the response we return, status and timing) to
/// IdfyRequestLogs, with images and PII redacted. Complements <see cref="IdfyLoggingHandler"/>,
/// which logs the outbound IDfy leg; the two share a TraceId. Only /api/* paths are logged.
/// </summary>
public sealed class RequestLoggingMiddleware(DbLogQueue queue, ILogger<RequestLoggingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var request = context.Request;
        request.EnableBuffering();
        var requestBody = await ReadBodyAsync(request.Body, context.RequestAborted);
        request.Body.Position = 0;

        // Capture what downstream writes so we can log it, then copy it on to the real stream.
        var originalBody = context.Response.Body;
        using var captured = new MemoryStream();
        context.Response.Body = captured;

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();

            var responseBody = ReadStream(captured);
            captured.Position = 0;
            try
            {
                await captured.CopyToAsync(originalBody, context.RequestAborted);
            }
            catch (OperationCanceledException) { /* client went away; nothing to flush */ }
            context.Response.Body = originalBody;

            // Logging must never break the request: swallow (but record) any redaction/enqueue fault.
            try
            {
                queue.Enqueue(new RequestLog
                {
                    CreatedAt = DateTimeOffset.UtcNow,
                    TraceId = Activity.Current?.TraceId.ToString(),
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    Method = request.Method,
                    Path = request.Path + request.QueryString,
                    StatusCode = context.Response.StatusCode,
                    RequestBody = LogRedaction.RedactInboundRequest(requestBody, request.ContentType),
                    ResponseBody = LogRedaction.MaskResponse(responseBody),
                    DurationMs = sw.ElapsedMilliseconds,
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to record inbound request log for {Path}.", request.Path);
            }
        }
    }

    private static async Task<string?> ReadBodyAsync(Stream body, CancellationToken ct)
    {
        using var reader = new StreamReader(body, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static string ReadStream(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
