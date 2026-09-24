using System.Diagnostics;
using Idfy.Api.Data;
using Microsoft.AspNetCore.Diagnostics;

namespace Idfy.Api.Logging;

/// <summary>
/// Records unhandled exceptions, then returns false so the default ProblemDetails response is still written.
/// </summary>
public sealed class DbExceptionHandler(DbLogQueue queue) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        queue.Enqueue(new ErrorLog
        {
            CreatedAt = DateTimeOffset.UtcNow,
            TraceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            Method = context.Request.Method,
            Path = context.Request.Path + context.Request.QueryString,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            Details = exception.ToString(),
        });

        return ValueTask.FromResult(false);
    }
}
