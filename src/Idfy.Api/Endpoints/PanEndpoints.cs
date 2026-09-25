using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class PanEndpoints
{
    public static IEndpointRouteBuilder MapPanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pan").WithTags("PAN");

        group.MapPost("/extract", Extract)
            .WithSummary("Extract PAN card details (OCR) from a URL or Base64 image.");

        group.MapPost("/extract/upload", ExtractUpload)
            .WithSummary("Extract PAN card details (OCR) from an uploaded image file.")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<IdfyTaskResponse<PanExtractionResult>>, ProblemHttpResult>> Extract(
        ExtractPanRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        if (CheckDocument(request.Document, options.Value.PanImageLimits, options.Value) is { } problem)
            return problem;

        return await SendAsync(idfy, request.Document, request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<PanExtractionResult>>, ProblemHttpResult>> ExtractUpload(
        [FromForm] FileUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var (base64, problem) = await ReadUploadAsync(form.File, options.Value.PanImageLimits, options.Value, ct);
        if (problem is not null)
            return problem;

        return await SendAsync(idfy, base64!, null, null, ct);
    }

    private static Task<Results<Ok<IdfyTaskResponse<PanExtractionResult>>, ProblemHttpResult>> SendAsync(
        IIdfyClient idfy, string document, Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (task, group) = NewIds(taskId, groupId);
        var request = new IdfyTaskRequest<IdfyDocumentData>(task, group, new IdfyDocumentData(document));
        return CallAsync(() => idfy.ExtractPanAsync(request, ct), ct);
    }
}
