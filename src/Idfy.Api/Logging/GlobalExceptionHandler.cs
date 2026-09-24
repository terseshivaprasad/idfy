using System.Diagnostics;
using Idfy.Api.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Idfy.Api.Logging;

/// <summary>
/// Single place all unhandled exceptions land. Client errors (e.g. a malformed body) become a 4xx
/// with a helpful message; anything else becomes a 500 whose detail is withheld from the caller and
/// recorded to ErrorLogs instead. Every response carries the trace id for correlation.
/// </summary>
public sealed class GlobalExceptionHandler(
    DbLogQueue queue,
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        // Body-binding failures (malformed JSON, duplicate keys, unknown members) surface as BadHttpRequestException.
        var (status, title, detail) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "Invalid request.",
                (exception.InnerException ?? exception).Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", (string?)null),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path} (trace {TraceId}).",
                context.Request.Method, context.Request.Path, traceId);

            queue.Enqueue(new ErrorLog
            {
                CreatedAt = DateTimeOffset.UtcNow,
                TraceId = traceId,
                Method = context.Request.Method,
                Path = context.Request.Path + context.Request.QueryString,
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message,
                Details = exception.ToString(),
            });
        }
        else
        {
            logger.LogWarning("Bad request for {Method} {Path}: {Message}",
                context.Request.Method, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = status >= 500 ? null : exception, // don't hand the exception to writers on 5xx
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["traceId"] = traceId },
            },
        });
    }
}
