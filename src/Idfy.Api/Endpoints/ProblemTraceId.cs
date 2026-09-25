using System.Diagnostics;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace Idfy.Api.Endpoints;

/// <summary>
/// Puts a <c>traceId</c> on every problem response, matching the TraceId column of the log tables.
/// On .NET 8, problem results returned by endpoints bypass IProblemDetailsService, so they are
/// stamped by <see cref="Filter"/>; everything else (status-code pages, the exception handler)
/// goes through <see cref="Customize"/>.
/// </summary>
internal static class ProblemTraceId
{
    public static string Get(HttpContext context) => Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    /// <summary>For AddProblemDetails(o => o.CustomizeProblemDetails = ...).</summary>
    public static void Customize(ProblemDetailsContext context) =>
        context.ProblemDetails.Extensions.TryAdd("traceId", Get(context.HttpContext));

    public static async ValueTask<object?> Filter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        // Typed handlers return Results<Ok<T>, ProblemHttpResult>; unwrap to the actual result.
        var inner = result is INestedHttpResult nested ? nested.Result : result;
        if (inner is IValueHttpResult { Value: ProblemDetails problem })
            problem.Extensions.TryAdd("traceId", Get(context.HttpContext));

        return result;
    }
}
