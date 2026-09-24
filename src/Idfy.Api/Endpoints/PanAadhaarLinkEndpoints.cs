using Idfy.Api.Models;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class PanAadhaarLinkEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapPanAadhaarLinkEndpoints()
        {
            var group = app.MapGroup("/api/pan-aadhaar-link").WithTags("PAN-Aadhaar Link");

            group.MapPost("/verify/sync", SyncVerify)
                .WithSummary("Check PAN-Aadhaar linkage synchronously (result returned directly).");

            group.MapPost("/verify", SubmitVerify)
                .WithSummary("Submit async PAN-Aadhaar link check; returns a requestId.");

            group.MapGet("/verify/{requestId}", PollVerify)
                .WithSummary("Poll an async PAN-Aadhaar link check by requestId. 202 while still processing.");

            return app;
        }
    }

    private static Task<Results<Ok<IdfyTaskResponse<PanAadhaarLinkResult>>, ProblemHttpResult>> SyncVerify(
        PanAadhaarLinkRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.VerifyPanAadhaarLinkAsync(BuildRequest(request), ct), ct);

    private static Task<Results<Ok<IdfyAsyncSubmitResponse>, ProblemHttpResult>> SubmitVerify(
        PanAadhaarLinkRequest request, IIdfyClient idfy, CancellationToken ct) =>
        CallAsync(() => idfy.SubmitPanAadhaarLinkAsync(BuildRequest(request), ct), ct);

    private static Task<IResult> PollVerify(string requestId, IIdfyClient idfy, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            var result = await idfy.GetPanAadhaarLinkAsync(requestId, ct);
            return result is null
                ? Results.Accepted(value: new { requestId, status = "processing" })
                : Results.Ok(result);
        }, ct);

    private static IdfyTaskRequest<IdfyPanAadhaarLinkData> BuildRequest(PanAadhaarLinkRequest request)
    {
        var (task, group) = NewIds(request.TaskId, request.GroupId);
        return new IdfyTaskRequest<IdfyPanAadhaarLinkData>(task, group,
            new IdfyPanAadhaarLinkData(request.PanNumber, request.AadhaarNumber));
    }
}
