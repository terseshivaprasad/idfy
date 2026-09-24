using Idfy.Api.Models;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class VoterIdEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapVoterIdEndpoints()
        {
            var group = app.MapGroup("/api/voter-id").WithTags("Voter ID");

            group.MapPost("/verify", SubmitVerify)
                .WithSummary("Submit async voter-id verification against the source; returns a requestId.");

            group.MapGet("/verify/{requestId}", PollVerify)
                .WithSummary("Poll an async voter-id verification by requestId. 202 while still processing.");

            return app;
        }
    }

    private static Task<Results<Ok<IdfyAsyncSubmitResponse>, ProblemHttpResult>> SubmitVerify(
        VerifyVoterIdRequest request, IIdfyClient idfy, CancellationToken ct)
    {
        var (task, group) = NewIds(request.TaskId, request.GroupId);
        var upstream = new IdfyTaskRequest<IdfyVoterIdVerifyData>(task, group, new IdfyVoterIdVerifyData(request.IdNumber));
        return CallAsync(() => idfy.SubmitVoterIdVerificationAsync(upstream, ct), ct);
    }

    private static Task<IResult> PollVerify(string requestId, IIdfyClient idfy, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            var result = await idfy.GetVoterIdVerificationAsync(requestId, ct);
            return result is null
                ? Results.Accepted(value: new { requestId, status = "processing" })
                : Results.Ok(result);
        }, ct);
}
