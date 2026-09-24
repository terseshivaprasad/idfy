using System.Net;
using System.Text.Json;
using Idfy.Api.Models;

namespace Idfy.Api.Services;

public interface IIdfyClient
{
    Task<IdfyTaskResponse<DocumentValidationResult>> ValidateDocumentAsync(
        IdfyTaskRequest<IdfyValidateDocumentData> request, CancellationToken ct = default);

    Task<IdfyTaskResponse<PanExtractionResult>> ExtractPanAsync(
        IdfyTaskRequest<IdfyDocumentData> request, CancellationToken ct = default);

    Task<IdfyTaskResponse<AadhaarExtractionResult>> ExtractAadhaarAsync(
        IdfyTaskRequest<IdfyAadhaarData> request, CancellationToken ct = default);

    Task<IdfyTaskResponse<DrivingLicenseResult>> ExtractDrivingLicenseAsync(
        IdfyTaskRequest<IdfyDocumentData> request, CancellationToken ct = default);

    Task<IdfyTaskResponse<PassportResult>> ExtractPassportAsync(
        IdfyTaskRequest<IdfyPassportData> request, CancellationToken ct = default);

    /// <summary>Verifies a driving licence against the source synchronously (result returned directly).</summary>
    Task<IdfyTaskResponse<DrivingLicenseSourceResult>> VerifyDrivingLicenseAsync(
        IdfyTaskRequest<IdfyDrivingLicenseVerifyData> request, CancellationToken ct = default);

    /// <summary>Submits the async driving-license verification; returns the request_id to poll with.</summary>
    Task<IdfyAsyncSubmitResponse> SubmitDrivingLicenseVerificationAsync(
        IdfyTaskRequest<IdfyDrivingLicenseVerifyData> request, CancellationToken ct = default);

    /// <summary>Polls an async task by request_id. Null means the result is not ready yet.</summary>
    Task<IdfyTaskResponse<DrivingLicenseSourceResult>?> GetDrivingLicenseVerificationAsync(
        string requestId, CancellationToken ct = default);

    /// <summary>Verifies a voter id against the source synchronously (result returned directly).</summary>
    Task<IdfyTaskResponse<VoterIdSourceResult>> VerifyVoterIdAsync(
        IdfyTaskRequest<IdfyVoterIdVerifyData> request, CancellationToken ct = default);

    /// <summary>Submits async voter-id verification; returns the request_id to poll with.</summary>
    Task<IdfyAsyncSubmitResponse> SubmitVoterIdVerificationAsync(
        IdfyTaskRequest<IdfyVoterIdVerifyData> request, CancellationToken ct = default);

    /// <summary>Polls an async voter-id task by request_id. Null means the result is not ready yet.</summary>
    Task<IdfyTaskResponse<VoterIdSourceResult>?> GetVoterIdVerificationAsync(
        string requestId, CancellationToken ct = default);
}

/// <summary>A non-success response from IDfy, with the error fields parsed when the body is JSON.</summary>
public sealed class IdfyApiException(HttpStatusCode statusCode, string responseBody)
    : Exception($"IDfy returned {(int)statusCode} ({statusCode}).")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>e.g. BAD_REQUEST, INSUFFICIENT_CREDITS, INVALID_URL, TIMEOUT. Null for HTML/empty bodies.</summary>
    public string? ErrorCode { get; } = ReadString(responseBody, "error", "error_code", "code");
    public string? ErrorMessage { get; } = ReadString(responseBody, "message", "error_message");
    public string? RequestId { get; } = ReadString(responseBody, "request_id");

    /// <summary>
    /// True when the caller can fix it (bad input, oversized payload, unusable image, rate limit).
    /// False for our problems: auth (401/403), INSUFFICIENT_CREDITS, and IDfy-side failures.
    /// </summary>
    public bool IsCallerError => (int)StatusCode switch
    {
        400 or 413 or 429 => true,
        422 => ErrorCode != "INSUFFICIENT_CREDITS",
        _ => false,
    };

    private static string? ReadString(string body, params ReadOnlySpan<string> names)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in names)
                if (doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
        }
        catch (JsonException)
        {
            // e.g. the HTML page IDfy renders for 502
        }
        return null;
    }
}

public sealed class IdfyClient(HttpClient http, ILogger<IdfyClient> logger) : IIdfyClient
{
    public Task<IdfyTaskResponse<DocumentValidationResult>> ValidateDocumentAsync(
        IdfyTaskRequest<IdfyValidateDocumentData> request, CancellationToken ct = default) =>
        PostAsync<IdfyValidateDocumentData, DocumentValidationResult>("v3/tasks/sync/validate/document", request, ct);

    public Task<IdfyTaskResponse<PanExtractionResult>> ExtractPanAsync(
        IdfyTaskRequest<IdfyDocumentData> request, CancellationToken ct = default) =>
        PostAsync<IdfyDocumentData, PanExtractionResult>("v3/tasks/sync/extract/ind_pan", request, ct);

    public Task<IdfyTaskResponse<AadhaarExtractionResult>> ExtractAadhaarAsync(
        IdfyTaskRequest<IdfyAadhaarData> request, CancellationToken ct = default) =>
        PostAsync<IdfyAadhaarData, AadhaarExtractionResult>("v3/tasks/sync/extract/ind_aadhaar", request, ct);

    public Task<IdfyTaskResponse<DrivingLicenseResult>> ExtractDrivingLicenseAsync(
        IdfyTaskRequest<IdfyDocumentData> request, CancellationToken ct = default) =>
        // This task returns the task object wrapped in a single-element array.
        PostAsync<IdfyDocumentData, DrivingLicenseResult>("v3/tasks/sync/extract/ind_driving_license", request, ct);

    public Task<IdfyTaskResponse<PassportResult>> ExtractPassportAsync(
        IdfyTaskRequest<IdfyPassportData> request, CancellationToken ct = default) =>
        PostAsync<IdfyPassportData, PassportResult>("v3/tasks/sync/extract/ind_passport", request, ct);

    public Task<IdfyTaskResponse<DrivingLicenseSourceResult>> VerifyDrivingLicenseAsync(
        IdfyTaskRequest<IdfyDrivingLicenseVerifyData> request, CancellationToken ct = default) =>
        PostAsync<IdfyDrivingLicenseVerifyData, DrivingLicenseSourceResult>(
            "v3/tasks/sync/verify_with_source/ind_driving_license", request, ct);

    public Task<IdfyAsyncSubmitResponse> SubmitDrivingLicenseVerificationAsync(
        IdfyTaskRequest<IdfyDrivingLicenseVerifyData> request, CancellationToken ct = default) =>
        SubmitAsync("v3/tasks/async/verify_with_source/ind_driving_license", request, ct);

    public Task<IdfyTaskResponse<DrivingLicenseSourceResult>?> GetDrivingLicenseVerificationAsync(
        string requestId, CancellationToken ct = default) =>
        GetTaskAsync<DrivingLicenseSourceResult>(requestId, ct);

    public Task<IdfyTaskResponse<VoterIdSourceResult>> VerifyVoterIdAsync(
        IdfyTaskRequest<IdfyVoterIdVerifyData> request, CancellationToken ct = default) =>
        PostAsync<IdfyVoterIdVerifyData, VoterIdSourceResult>(
            "v3/tasks/sync/verify_with_source/ind_voter_id", request, ct);

    public Task<IdfyAsyncSubmitResponse> SubmitVoterIdVerificationAsync(
        IdfyTaskRequest<IdfyVoterIdVerifyData> request, CancellationToken ct = default) =>
        SubmitAsync("v3/tasks/async/verify_with_source/ind_voter_id", request, ct);

    public Task<IdfyTaskResponse<VoterIdSourceResult>?> GetVoterIdVerificationAsync(
        string requestId, CancellationToken ct = default) =>
        GetTaskAsync<VoterIdSourceResult>(requestId, ct);

    private async Task<IdfyAsyncSubmitResponse> SubmitAsync<TData>(
        string path, IdfyTaskRequest<TData> request, CancellationToken ct)
    {
        var (body, status) = await PostRawAsync(path, request, ct);
        try
        {
            return JsonSerializer.Deserialize<IdfyAsyncSubmitResponse>(body) ?? throw new IdfyApiException(status, body);
        }
        catch (JsonException)
        {
            throw new IdfyApiException(status, body);
        }
    }

    /// <summary>Polls GET /v3/tasks?request_id=. Null when the result is not ready (empty array).</summary>
    private async Task<IdfyTaskResponse<TResult>?> GetTaskAsync<TResult>(string requestId, CancellationToken ct)
    {
        var path = $"v3/tasks?request_id={Uri.EscapeDataString(requestId)}";
        using var response = await http.GetAsync(path, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw Fail(path, null, response.StatusCode, body);

        return DeserializeOrNull<TResult>(body, response.StatusCode);
    }

    private async Task<IdfyTaskResponse<TResult>> PostAsync<TData, TResult>(
        string path, IdfyTaskRequest<TData> request, CancellationToken ct)
    {
        var (body, status) = await PostRawAsync(path, request, ct);
        return Deserialize<TResult>(body, status);
    }

    private async Task<(string Body, HttpStatusCode Status)> PostRawAsync<TData>(
        string path, IdfyTaskRequest<TData> request, CancellationToken ct)
    {
        // Buffer so the request carries Content-Length instead of chunked encoding.
        using var content = JsonContent.Create(request);
        await content.LoadIntoBufferAsync(ct);
        using var response = await http.PostAsync(path, content, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw Fail(path, request.TaskId, response.StatusCode, body);

        return (body, response.StatusCode);
    }

    private IdfyApiException Fail(string path, string? taskId, HttpStatusCode status, string body)
    {
        var ex = new IdfyApiException(status, body);
        logger.Log(ex.IsCallerError ? LogLevel.Warning : LogLevel.Error,
            "IDfy {Path} failed for task {TaskId}: {Status} {ErrorCode} {Body}",
            path, taskId, (int)status, ex.ErrorCode, body);
        return ex;
    }

    /// <summary>Like <see cref="Deserialize{TResult}"/> but returns null for an empty array (task not ready).</summary>
    private static IdfyTaskResponse<TResult>? DeserializeOrNull<TResult>(string body, HttpStatusCode statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() == 0)
                return null;
        }
        catch (JsonException)
        {
            throw new IdfyApiException(statusCode, body);
        }

        return Deserialize<TResult>(body, statusCode);
    }

    /// <summary>Some tasks (e.g. driving license) wrap the task object in a single-element array.</summary>
    private static IdfyTaskResponse<TResult> Deserialize<TResult>(string body, HttpStatusCode statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var element = doc.RootElement;
            if (element.ValueKind == JsonValueKind.Array)
            {
                var first = element.EnumerateArray();
                if (!first.MoveNext())
                    throw new IdfyApiException(statusCode, body);
                element = first.Current;
            }

            return element.Deserialize<IdfyTaskResponse<TResult>>()
                   ?? throw new IdfyApiException(statusCode, body);
        }
        catch (JsonException)
        {
            throw new IdfyApiException(statusCode, body);
        }
    }
}
