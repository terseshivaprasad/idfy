using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class FaceEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapFaceEndpoints()
        {
            var group = app.MapGroup("/api/face").WithTags("Face");

            group.MapPost("/compare", Compare)
                .WithSummary("Compare two face images (URL/Base64) and return a match result.");

            group.MapPost("/compare/upload", CompareUpload)
                .WithSummary("Compare two uploaded face image files and return a match result.")
                .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
                .DisableAntiforgery();

            return app;
        }
    }

    private static async Task<Results<Ok<IdfyTaskResponse<FaceCompareResult>>, ProblemHttpResult>> Compare(
        CompareFaceRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var limits = options.Value.FaceMatchImageLimits;
        if (CheckDocument(request.Document, limits, options.Value) is { } p1)
            return p1;
        if (CheckDocument(request.Document2, limits, options.Value) is { } p2)
            return p2;

        return await SendAsync(idfy, request.Document, request.Document2, request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<FaceCompareResult>>, ProblemHttpResult>> CompareUpload(
        [FromForm] FaceCompareUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var limits = options.Value.FaceMatchImageLimits;

        var (image1, p1) = await ReadUploadAsync(form.File, limits, options.Value, ct);
        if (p1 is not null)
            return p1;
        var (image2, p2) = await ReadUploadAsync(form.File2, limits, options.Value, ct);
        if (p2 is not null)
            return p2;

        return await SendAsync(idfy, image1!, image2!, null, null, ct);
    }

    private static Task<Results<Ok<IdfyTaskResponse<FaceCompareResult>>, ProblemHttpResult>> SendAsync(
        IIdfyClient idfy, string document1, string document2, Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (task, group) = NewIds(taskId, groupId);
        var request = new IdfyTaskRequest<IdfyFaceCompareData>(task, group, new IdfyFaceCompareData(document1, document2));
        return CallAsync(() => idfy.CompareFacesAsync(request, ct), ct);
    }
}

public sealed class FaceCompareUploadForm : FileUploadForm
{
    /// <summary>Second image to compare against <see cref="FileUploadForm.File"/>.</summary>
    public IFormFile? File2 { get; set; }
}
