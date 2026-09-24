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

            group.MapPost("/verify/sync", SyncVerify)
                .WithSummary("Verify a passport against the source synchronously (result returned directly).");

            group.MapPost("/verify", SubmitVerify)
                .WithSummary("Submit async passport verification against the source; returns a requestId.");

            group.MapGet("/verify/{requestId}", PollVerify)
                .WithSummary("Poll an async passport verification by requestId. 202 while still processing.");

            return app;
        }
    }

    private static Task<Results<Ok<IdfyTaskResponse<PassportSourceResult>>, ProblemHttpResult>> SyncVerify(
        VerifyPassportRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.VerifyPassportAsync(BuildVerifyRequest(request), ct), ct);

    private static Task<Results<Ok<IdfyAsyncSubmitResponse>, ProblemHttpResult>> SubmitVerify(
        VerifyPassportRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.SubmitPassportVerificationAsync(BuildVerifyRequest(request), ct), ct);

    private static Task<IResult> PollVerify(string requestId, IIdfyClient idfy, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            var result = await idfy.GetPassportVerificationAsync(requestId, ct);
            return result is null
                ? Results.Accepted(value: new { requestId, status = "processing" })
                : Results.Ok(result);
        }, ct);

    private static IdfyTaskRequest<IdfyPassportVerifyData> BuildVerifyRequest(VerifyPassportRequest request)
    {
        var (task, group) = NewIds(request.TaskId, request.GroupId);
        return new IdfyTaskRequest<IdfyPassportVerifyData>(task, group,
            new IdfyPassportVerifyData(
                request.PassportFileNumber,
                request.DateOfBirth!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
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
