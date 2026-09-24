using System.Text.Json.Nodes;

namespace Idfy.Api.Logging;

/// <summary>
/// The non-PII envelope fields of an IDfy task, pulled from the request and response bodies for the
/// structured IdfyTasks table. Never touches extraction_output.
/// </summary>
public sealed record IdfyTaskMetadata(
    string? TaskId,
    string? GroupId,
    string? RequestId,
    string? TaskType,
    string? Action,
    string? Status,
    string? ErrorCode,
    DateTimeOffset? IdfyCreatedAt,
    DateTimeOffset? IdfyCompletedAt)
{
    /// <summary>String entry point (parses); see the JsonNode overload.</summary>
    public static IdfyTaskMetadata Parse(string? requestBody, string? responseBody) =>
        From(LogRedaction.TryParse(requestBody), LogRedaction.TryParse(responseBody));

    public static IdfyTaskMetadata From(JsonNode? request, JsonNode? response)
    {
        // Driving-license and passport wrap the task object in a single-element array.
        var resp = response is JsonArray array ? (array.Count > 0 ? array[0] : null) : response;

        return new IdfyTaskMetadata(
            // Prefer the ids echoed by IDfy, fall back to what we sent.
            TaskId: Str(resp, "task_id") ?? Str(request, "task_id"),
            GroupId: Str(resp, "group_id") ?? Str(request, "group_id"),
            RequestId: Str(resp, "request_id"),
            TaskType: Str(resp, "type"),
            Action: Str(resp, "action"),
            Status: Str(resp, "status"),
            ErrorCode: Str(resp, "error") ?? Str(resp, "error_code") ?? Str(resp, "code"),
            IdfyCreatedAt: Date(resp, "created_at"),
            IdfyCompletedAt: Date(resp, "completed_at"));
    }

    private static string? Str(JsonNode? node, string name) =>
        node is JsonObject o && o.TryGetPropertyValue(name, out var v)
            && v is JsonValue jv && jv.TryGetValue<string>(out var s)
            ? s
            : null;

    private static DateTimeOffset? Date(JsonNode? node, string name) =>
        Str(node, name) is { } s && DateTimeOffset.TryParse(s, out var d) ? d : null;
}
