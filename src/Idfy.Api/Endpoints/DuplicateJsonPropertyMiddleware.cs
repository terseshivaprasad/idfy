using System.Text.Json;

namespace Idfy.Api.Endpoints;

/// <summary>
/// Rejects JSON request bodies that repeat a property (e.g. two "document" keys). System.Text.Json on
/// .NET 8 would otherwise silently keep the last value.
/// </summary>
public sealed class DuplicateJsonPropertyMiddleware(IProblemDetailsService problemDetails) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api") && request.HasJsonContentType())
        {
            request.EnableBuffering();
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, context.RequestAborted);
            request.Body.Position = 0;

            if (FindDuplicate(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) is { } name)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = { Title = "Invalid request.", Detail = $"Duplicate JSON property '{name}'." },
                });
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// First property name repeated within one object, or null. Case-insensitive, like request binding.
    /// Malformed JSON returns null and is left to the binder to reject.
    /// </summary>
    internal static string? FindDuplicate(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        var scopes = new Stack<HashSet<string>?>(); // null for arrays

        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        scopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                        break;
                    case JsonTokenType.StartArray:
                        scopes.Push(null);
                        break;
                    case JsonTokenType.EndObject or JsonTokenType.EndArray:
                        scopes.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        var name = reader.GetString()!;
                        if (!scopes.Peek()!.Add(name))
                            return name;
                        break;
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
