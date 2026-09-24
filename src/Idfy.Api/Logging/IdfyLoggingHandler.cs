using System.Diagnostics;
using Idfy.Api.Data;

namespace Idfy.Api.Logging;

/// <summary>
/// Records every IDfy call (request, response or exception, duration) to the log database,
/// with document images and extracted personal details redacted.
/// </summary>
public sealed class IdfyLoggingHandler(DbLogQueue queue) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var requestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        var (taskId, groupId, loggedBody) = LogRedaction.RedactRequest(requestBody);

        var log = new ApiCallLog
        {
            CreatedAt = DateTimeOffset.UtcNow,
            TraceId = Activity.Current?.TraceId.ToString(),
            TaskId = taskId,
            GroupId = groupId,
            Method = request.Method.Method,
            Url = request.RequestUri?.ToString() ?? string.Empty,
            RequestBody = loggedBody,
        };

        string? rawResponse = null;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, ct);
            // Buffer so the body can be read here and again by the caller.
            await response.Content.LoadIntoBufferAsync(ct);
            log.StatusCode = (int)response.StatusCode;
            rawResponse = await response.Content.ReadAsStringAsync(ct);
            log.ResponseBody = LogRedaction.MaskResponse(rawResponse);
            return response;
        }
        catch (Exception ex)
        {
            log.Exception = ex.ToString();
            throw;
        }
        finally
        {
            log.DurationMs = sw.ElapsedMilliseconds;
            queue.Enqueue(log);
            queue.Enqueue(BuildTaskLog(log, requestBody, rawResponse));
        }
    }

    /// <summary>Structured, PII-free row for the IdfyTasks table, parsed from the same envelope.</summary>
    private static IdfyTaskLog BuildTaskLog(ApiCallLog log, string? requestBody, string? rawResponse)
    {
        var meta = IdfyTaskMetadata.Parse(requestBody, rawResponse);
        return new IdfyTaskLog
        {
            CreatedAt = log.CreatedAt,
            TraceId = log.TraceId,
            TaskId = meta.TaskId,
            GroupId = meta.GroupId,
            RequestId = meta.RequestId,
            TaskType = meta.TaskType,
            Action = meta.Action,
            Status = meta.Status,
            HttpStatus = log.StatusCode,
            ErrorCode = meta.ErrorCode,
            DurationMs = log.DurationMs,
            IdfyCreatedAt = meta.IdfyCreatedAt,
            IdfyCompletedAt = meta.IdfyCompletedAt,
        };
    }
}
