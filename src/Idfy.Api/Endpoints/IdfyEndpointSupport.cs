using System.Net;
using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Idfy.Api.Endpoints;

/// <summary>Input checks, upload handling and IDfy error mapping shared by all IDfy endpoints.</summary>
internal static class IdfyEndpointSupport
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    public static (string TaskId, string GroupId) NewIds(Guid? taskId, Guid? groupId) =>
        ((taskId ?? Guid.NewGuid()).ToString(), (groupId ?? Guid.NewGuid()).ToString());

    /// <summary>Checks an inline Base64 image; URLs are fetched by IDfy, so they pass through.</summary>
    public static ProblemHttpResult? CheckDocument(string document, ImageLimits limits, IdfyOptions options)
    {
        if (UrlOrBase64Attribute.IsUrl(document))
            return null;

        return CheckBase64Size(document.Length, options)
               ?? CheckResolution(Convert.FromBase64String(document), limits);
    }

    /// <summary>Validates an uploaded image and returns it as Base64, or the problem to return.</summary>
    public static async Task<(string? Base64, ProblemHttpResult? Problem)> ReadUploadAsync(
        IFormFile? file, ImageLimits limits, IdfyOptions options, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
            return (null, TypedResults.Problem("Uploaded file is empty.", statusCode: StatusCodes.Status400BadRequest));

        if (file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
            return (null, TypedResults.Problem($"Uploaded file must be an image, got '{file.ContentType}'.",
                statusCode: StatusCodes.Status415UnsupportedMediaType));

        // Base64 grows the payload by 4/3.
        if (CheckBase64Size((file.Length + 2) / 3 * 4, options) is { } tooLarge)
            return (null, tooLarge);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var image = ms.GetBuffer().AsSpan(0, (int)ms.Length);

        if (CheckResolution(image, limits) is { } badSize)
            return (null, badSize);

        return (Convert.ToBase64String(image), null);
    }

    /// <summary>Runs an IDfy call and turns failures into ProblemDetails.</summary>
    public static async Task<Results<Ok<T>, ProblemHttpResult>> CallAsync<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await call());
        }
        catch (IdfyApiException ex)
        {
            return MapIdfyError(ex);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return TypedResults.Problem("IDfy request timed out.", statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException ex)
        {
            return TypedResults.Problem($"Could not reach IDfy: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>Like <see cref="CallAsync{T}"/> but the action returns the final IResult (e.g. Ok/Accepted).</summary>
    public static async Task<IResult> GuardAsync(Func<Task<IResult>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (IdfyApiException ex)
        {
            return MapIdfyError(ex);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return TypedResults.Problem("IDfy request timed out.", statusCode: StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException ex)
        {
            return TypedResults.Problem($"Could not reach IDfy: {ex.Message}", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Caller-fixable IDfy errors keep their status and IDfy's code/message. Our own problems
    /// (credentials, credits) and IDfy outages become 502 without internal detail; they are logged
    /// and recorded in ApiCallLogs. IDfy's own 504 (processing over 50s) stays 504.
    /// </summary>
    private static ProblemHttpResult MapIdfyError(IdfyApiException ex)
    {
        var (status, title, detail) = ex switch
        {
            { IsCallerError: true } => ((int)ex.StatusCode,
                string.IsNullOrEmpty(ex.ErrorCode) ? ex.StatusCode.ToString() : ex.ErrorCode, ex.ErrorMessage),
            { StatusCode: HttpStatusCode.GatewayTimeout } =>
                (StatusCodes.Status504GatewayTimeout, "TIMEOUT", "IDfy took too long to process the document."),
            _ => (StatusCodes.Status502BadGateway, "UPSTREAM_ERROR", "The document verification service is unavailable."),
        };

        return TypedResults.Problem(
            title: title,
            detail: detail,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["idfyStatus"] = (int)ex.StatusCode,
                ["idfyRequestId"] = ex.RequestId,
            });
    }

    private static ProblemHttpResult? CheckBase64Size(long base64Length, IdfyOptions options) =>
        base64Length <= options.MaxBase64Length
            ? null
            : TypedResults.Problem(
                title: "Payload too large.",
                detail: $"Image is {base64Length} chars as Base64; IDfy accepts at most {options.MaxBase64Length}. " +
                        "Compress or resize the image, or pass a URL instead.",
                statusCode: StatusCodes.Status413PayloadTooLarge);

    /// <summary>
    /// Rejects JPEG/PNG images outside IDfy's resolution limits. Other formats pass through for IDfy to judge.
    /// </summary>
    private static ProblemHttpResult? CheckResolution(ReadOnlySpan<byte> image, ImageLimits limits)
    {
        if (!ImageDimensions.TryGetSize(image, out var width, out var height))
            return null;

        if (Math.Min(width, height) >= limits.MinDimension && Math.Max(width, height) <= limits.MaxDimension)
            return null;

        return TypedResults.Problem(
            title: "Image resolution out of range.",
            detail: $"Image is {width}x{height}px; width and height must each be between " +
                    $"{limits.MinDimension} and {limits.MaxDimension}px.",
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}

public class FileUploadForm
{
    public IFormFile? File { get; set; }
}
