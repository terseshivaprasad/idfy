using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class DrivingLicenseEndpoints
{
    public static IEndpointRouteBuilder MapDrivingLicenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/driving-license").WithTags("Driving License");

        group.MapPost("/extract", Extract)
            .WithSummary("Extract driving-license details (OCR) from a URL or Base64 image.");

        group.MapPost("/extract/upload", ExtractUpload)
            .WithSummary("Extract driving-license details (OCR) from an uploaded image file.")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();

        group.MapPost("/verify/sync", SyncVerify)
            .WithSummary("Verify a driving licence against the government source synchronously (result returned directly).");

        group.MapPost("/verify", SubmitVerify)
            .WithSummary("Submit async driving-license verification against the government source; returns a requestId.");

        group.MapGet("/verify/{requestId}", PollVerify)
            .WithSummary("Poll an async driving-license verification by requestId. 202 while still processing.");

        return app;
    }

    private static Task<Results<Ok<IdfyTaskResponse<DrivingLicenseSourceResult>>, ProblemHttpResult>> SyncVerify(
        VerifyDrivingLicenseRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.VerifyDrivingLicenseAsync(BuildVerifyRequest(request), ct), ct);

    private static Task<Results<Ok<IdfyAsyncSubmitResponse>, ProblemHttpResult>> SubmitVerify(
        VerifyDrivingLicenseRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.SubmitDrivingLicenseVerificationAsync(BuildVerifyRequest(request), ct), ct);

    private static IdfyTaskRequest<IdfyDrivingLicenseVerifyData> BuildVerifyRequest(VerifyDrivingLicenseRequest request)
    {
        var (task, group) = NewIds(request.TaskId, request.GroupId);
        var advanced = request.StateInfo || request.AgeInfo
            ? new IdfyDlVerifyAdvancedDetails(
                request.StateInfo ? true : null,
                request.AgeInfo ? true : null)
            : null;

        return new IdfyTaskRequest<IdfyDrivingLicenseVerifyData>(task, group,
            new IdfyDrivingLicenseVerifyData(
                request.IdNumber,
                request.DateOfBirth!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                advanced));
    }

    private static Task<IResult> PollVerify(string requestId, IIdfyClient idfy, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            var result = await idfy.GetDrivingLicenseVerificationAsync(requestId, ct);
            return result is null
                ? Results.Accepted(value: new { requestId, status = "processing" })
                : Results.Ok(result);
        }, ct);

    private static async Task<Results<Ok<IdfyTaskResponse<DrivingLicenseResult>>, ProblemHttpResult>> Extract(
        ExtractDrivingLicenseRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        if (CheckDocument(request.Document, options.Value.DrivingLicenseImageLimits, options.Value) is { } problem)
            return problem;

        return await SendAsync(idfy, request.Document, request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<DrivingLicenseResult>>, ProblemHttpResult>> ExtractUpload(
        [FromForm] FileUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var (base64, problem) = await ReadUploadAsync(form.File, options.Value.DrivingLicenseImageLimits, options.Value, ct);
        if (problem is not null)
            return problem;

        return await SendAsync(idfy, base64!, null, null, ct);
    }

    private static Task<Results<Ok<IdfyTaskResponse<DrivingLicenseResult>>, ProblemHttpResult>> SendAsync(
        IIdfyClient idfy, string document, Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (task, group) = NewIds(taskId, groupId);
        var request = new IdfyTaskRequest<IdfyDocumentData>(task, group, new IdfyDocumentData(document));
        return CallAsync(() => idfy.ExtractDrivingLicenseAsync(request, ct), ct);
    }
}
