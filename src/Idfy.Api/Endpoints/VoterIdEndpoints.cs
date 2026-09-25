using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class VoterIdEndpoints
{
    public static IEndpointRouteBuilder MapVoterIdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/voter-id").WithTags("Voter ID");

        group.MapPost("/extract", Extract)
            .WithSummary("Extract voter-id details (OCR) from a URL or Base64 image. Optional second (back) side.");

        group.MapPost("/extract/upload", ExtractUpload)
            .WithSummary("Extract voter-id details (OCR) from uploaded image file(s). Optional second (back) side.")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();

        group.MapPost("/verify/sync", SyncVerify)
            .WithSummary("Verify a voter id against the source synchronously (result returned directly).");

        group.MapPost("/verify", SubmitVerify)
            .WithSummary("Submit async voter-id verification against the source; returns a requestId.");

        group.MapGet("/verify/{requestId}", PollVerify)
            .WithSummary("Poll an async voter-id verification by requestId. 202 while still processing.");

        return app;
    }

    private static Task<Results<Ok<IdfyTaskResponse<VoterIdSourceResult>>, ProblemHttpResult>> SyncVerify(
        VerifyVoterIdRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.VerifyVoterIdAsync(BuildRequest(request), ct), ct);

    private static Task<Results<Ok<IdfyAsyncSubmitResponse>, ProblemHttpResult>> SubmitVerify(
        VerifyVoterIdRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.SubmitVoterIdVerificationAsync(BuildRequest(request), ct), ct);

    private static IdfyTaskRequest<IdfyVoterIdVerifyData> BuildRequest(VerifyVoterIdRequest request)
    {
        var (task, group) = NewIds(request.TaskId, request.GroupId);
        return new IdfyTaskRequest<IdfyVoterIdVerifyData>(task, group, new IdfyVoterIdVerifyData(request.IdNumber));
    }

    private static Task<IResult> PollVerify(string requestId, IIdfyClient idfy, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            var result = await idfy.GetVoterIdVerificationAsync(requestId, ct);
            return result is null
                ? Results.Accepted(value: new { requestId, status = "processing" })
                : Results.Ok(result);
        }, ct);

    private static async Task<Results<Ok<IdfyTaskResponse<VoterIdResult>>, ProblemHttpResult>> Extract(
        ExtractVoterIdRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var limits = options.Value.VoterIdImageLimits;

        if (CheckDocument(request.Document, limits, options.Value) is { } problem1)
            return problem1;

        var document2 = string.IsNullOrWhiteSpace(request.Document2) ? null : request.Document2;
        if (document2 is not null && CheckDocument(document2, limits, options.Value) is { } problem2)
            return problem2;

        return await SendAsync(idfy, request.Document, document2, request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<VoterIdResult>>, ProblemHttpResult>> ExtractUpload(
        [FromForm] VoterIdUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var limits = options.Value.VoterIdImageLimits;

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

    private static Task<Results<Ok<IdfyTaskResponse<VoterIdResult>>, ProblemHttpResult>> SendAsync(
        IIdfyClient idfy, string document1, string? document2, Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (task, group) = NewIds(taskId, groupId);
        var request = new IdfyTaskRequest<IdfyVoterIdData>(task, group, new IdfyVoterIdData(document1, document2));
        return CallAsync(() => idfy.ExtractVoterIdAsync(request, ct), ct);
    }
}

public sealed class VoterIdUploadForm : FileUploadForm
{
    /// <summary>Optional back side.</summary>
    public IFormFile? File2 { get; set; }
}
