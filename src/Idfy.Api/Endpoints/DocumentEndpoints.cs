using Idfy.Api.Models;
using Idfy.Api.Options;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static Idfy.Api.Endpoints.IdfyEndpointSupport;

namespace Idfy.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        group.MapPost("/validate", Validate)
            .WithSummary("Validate a document given as a URL or Base64 string.");

        group.MapPost("/validate/upload", ValidateUpload)
            .WithSummary("Validate an uploaded image file (converted to Base64 before forwarding).")
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<IdfyTaskResponse<DocumentValidationResult>>, ProblemHttpResult>> Validate(
        ValidateDocumentRequest request, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        if (CheckDocument(request.Document, options.Value.DocumentImageLimits, options.Value) is { } problem)
            return problem;

        return await SendAsync(idfy, options.Value, request.Document, request.DocType,
            new FeatureFlags(request.DetectDocSide, request.DetectFace, request.DetectScanned),
            request.TaskId, request.GroupId, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<DocumentValidationResult>>, ProblemHttpResult>> ValidateUpload(
        [FromForm] ValidateUploadForm form, IIdfyClient idfy, IOptions<IdfyOptions> options, CancellationToken ct)
    {
        var (base64, problem) = await ReadUploadAsync(form.File, options.Value.DocumentImageLimits, options.Value, ct);
        if (problem is not null)
            return problem;

        return await SendAsync(idfy, options.Value, base64!, form.DocType,
            new FeatureFlags(form.DetectDocSide, form.DetectFace, form.DetectScanned), null, null, ct);
    }

    private static async Task<Results<Ok<IdfyTaskResponse<DocumentValidationResult>>, ProblemHttpResult>> SendAsync(
        IIdfyClient idfy, IdfyOptions options, string document, string? docType, FeatureFlags features,
        Guid? taskId, Guid? groupId, CancellationToken ct)
    {
        var (advancedFeatures, unconfigured) = BuildAdvancedFeatures(features, options.AdvancedFeatureKeys);
        if (unconfigured.Count > 0)
            return TypedResults.Problem(
                title: "Advanced feature not configured.",
                detail: $"No IDfy key configured for: {string.Join(", ", unconfigured)}. " +
                        "Set Idfy:AdvancedFeatureKeys (key names are provided by the IDfy sales SPOC).",
                statusCode: StatusCodes.Status501NotImplemented);

        var (task, group) = NewIds(taskId, groupId);
        var request = new IdfyTaskRequest<IdfyValidateDocumentData>(task, group,
            new IdfyValidateDocumentData(document, docType, advancedFeatures));

        return await CallAsync(() => idfy.ValidateDocumentAsync(request, ct), ct);
    }

    /// <summary>Maps requested features to IDfy's key names; only requested features are sent.</summary>
    private static (Dictionary<string, bool>? Features, List<string> Unconfigured) BuildAdvancedFeatures(
        FeatureFlags requested, AdvancedFeatureKeys keys)
    {
        var features = new Dictionary<string, bool>();
        var unconfigured = new List<string>();

        void Add(bool enabled, string? key, string name)
        {
            if (!enabled) return;
            if (string.IsNullOrWhiteSpace(key)) unconfigured.Add(name);
            else features[key] = true;
        }

        Add(requested.DocSide, keys.DocSide, nameof(AdvancedFeatureKeys.DocSide));
        Add(requested.Face, keys.Face, nameof(AdvancedFeatureKeys.Face));
        Add(requested.Scanned, keys.Scanned, nameof(AdvancedFeatureKeys.Scanned));

        return (features.Count > 0 ? features : null, unconfigured);
    }

    private readonly record struct FeatureFlags(bool DocSide, bool Face, bool Scanned);
}

public sealed class ValidateUploadForm : FileUploadForm
{
    [DocType]
    public string? DocType { get; set; }

    public bool DetectDocSide { get; set; }
    public bool DetectFace { get; set; }
    public bool DetectScanned { get; set; }
}
