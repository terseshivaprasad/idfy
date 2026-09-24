using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class PassportEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapPassportEndpoints()
        {
            var group = app.MapGroup("/api/passport").WithTags("Passport");

            group.MapPost("/extract", Extract)
                .WithSummary("Extract passport details (OCR) from a URL or Base64 image. Optional second (back) page.");

            group.MapPost("/extract/upload", ExtractUpload)
                .WithSummary("Extract passport details (OCR) from uploaded image file(s). Optional second (back) page.")
                .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
                .DisableAntiforgery();

            return app;
        }
    }

    private static async Task<Results<Ok<IdfyTaskResponse<PassportResult>>, ProblemHttpResult>> Extract(
        ExtractPassportRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var limits = options.Value.PassportImageLimits;

        if (CheckDocument(request.Document, limits, options.Value) is { } problem1)
            return problem1;

        var document2 = string.IsNullOrWhiteSpace(request.Document2) ? null : request.Document2;
        if (document2 is not null && CheckDocument(document2, limits, options.Value) is { } problem2)
            return problem2;

        return await SendAsync(idfy, request.Document, document2, request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<PassportResult>>, ProblemHttpResult>> ExtractUpload(
        [FromForm] PassportUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var limits = options.Value.PassportImageLimits;

        var (document1, problem1) = await ReadUploadAsync(form.File, limits, options.Value, ct);
        if (problem1 is not null)
            return problem1;

        string? document2 = null;
        if (form.File2 is { Length: > 0 })
        {
            var (base64, problem2) = await ReadUploadAsync(form.File2, limits, options.Value, ct);
            if (problem2 is not null)
                return problem2;
            document2 = base64;
        }

        return await SendAsync(idfy, document1!, document2, null, null, ct);
    }

    private static Task<Results<Ok<IdfyTaskResponse<PassportResult>>, ProblemHttpResult>> SendAsync(
        IIdfyClient idfy, string document1, string? document2, Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (task, group) = NewIds(taskId, groupId);
        var request = new IdfyTaskRequest<IdfyPassportData>(task, group, new IdfyPassportData(document1, document2));
        return CallAsync(() => idfy.ExtractPassportAsync(request, ct), ct);
    }
}

public sealed class PassportUploadForm : FileUploadForm
{
    /// <summary>Optional back page.</summary>
    public IFormFile? File2 { get; set; }
}
