using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class AadhaarEndpoints
{
    // Aadhaar consent is mandatory; the upload form has no consent field, so it defaults to explicit opt-in.
    private const string Consent = "yes";

    public static IEndpointRouteBuilder MapAadhaarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/aadhaar").WithTags("Aadhaar");

        group.MapPost("/extract", Extract)
            .WithSummary("Extract Aadhaar details (OCR) from a URL or Base64 image. Requires consent.");

        group.MapPost("/extract/upload", ExtractUpload)
            .WithSummary("Extract Aadhaar details (OCR) from an uploaded image file. Requires consent.")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();

        group.MapPost("/mask", Mask)
            .WithSummary("Mask the Aadhaar number in a document image (URL/Base64). Requires consent.");

        group.MapPost("/mask/upload", MaskUpload)
            .WithSummary("Mask the Aadhaar number in an uploaded image file. Requires consent.")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<IdfyTaskResponse<MaskResult>>, ProblemHttpResult>> Mask(
        MaskAadhaarRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        if (CheckDocument(request.Document, options.Value.MaskImageLimits, options.Value) is { } problem)
            return problem;

        return await SendMaskAsync(idfy, request.Document, request.AdvancedFeatures, request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<MaskResult>>, ProblemHttpResult>> MaskUpload(
        [FromForm] AadhaarUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        if (form.Consent != true)
            return TypedResults.Problem("Consent must be given (consent=true) to process an Aadhaar document.",
                statusCode: StatusCodes.Status400BadRequest);

        var (base64, problem) = await ReadUploadAsync(form.File, options.Value.MaskImageLimits, options.Value, ct);
        if (problem is not null)
            return problem;

        return await SendMaskAsync(idfy, base64!, null, null, null, ct);
    }

    private static Task<Results<Ok<IdfyTaskResponse<MaskResult>>, ProblemHttpResult>> SendMaskAsync(
        IIdfyClient idfy, string document, Dictionary<string, bool>? advancedFeatures, Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (task, group) = NewIds(taskId, groupId);
        var data = new IdfyMaskAadhaarData(document, Consent)
        {
            AdvancedFeatures = advancedFeatures is { Count: > 0 }
                ? advancedFeatures.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
                : null,
        };
        return CallAsync(() => idfy.MaskAadhaarAsync(new IdfyTaskRequest<IdfyMaskAadhaarData>(task, group, data), ct), ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<AadhaarExtractionResult>>, ProblemHttpResult>> Extract(
        ExtractAadhaarRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        if (CheckDocument(request.Document, options.Value.AadhaarImageLimits, options.Value) is { } problem)
            return problem;

        return await SendAsync(idfy, request.Document, request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<AadhaarExtractionResult>>, ProblemHttpResult>> ExtractUpload(
        [FromForm] AadhaarUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        if (form.Consent != true)
            return TypedResults.Problem("Consent must be given (consent=true) to process an Aadhaar document.",
                statusCode: StatusCodes.Status400BadRequest);

        var (base64, problem) = await ReadUploadAsync(form.File, options.Value.AadhaarImageLimits, options.Value, ct);
        if (problem is not null)
            return problem;

        return await SendAsync(idfy, base64!, null, null, ct);
    }

    private static Task<Results<Ok<IdfyTaskResponse<AadhaarExtractionResult>>, ProblemHttpResult>> SendAsync(
        IIdfyClient idfy, string document, Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (task, group) = NewIds(taskId, groupId);
        var request = new IdfyTaskRequest<IdfyAadhaarData>(task, group, new IdfyAadhaarData(document, Consent));
        return CallAsync(() => idfy.ExtractAadhaarAsync(request, ct), ct);
    }
}

public sealed class AadhaarUploadForm : FileUploadForm
{
    /// <summary>Must be true: the Aadhaar holder has consented to this extraction.</summary>
    public bool? Consent { get; set; }
}
