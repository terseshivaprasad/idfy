using System.Text.Json;

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
    public static IdfyTaskMetadata Parse(string? requestBody, string? responseBody)
    {
        var (taskId, groupId) = ReadRequest(requestBody);
        var response = ReadResponse(responseBody);

        return new IdfyTaskMetadata(
            // Prefer the ids echoed by IDfy, fall back to what we sent.
            TaskId: response.TaskId ?? taskId,
            GroupId: response.GroupId ?? groupId,
            RequestId: response.RequestId,
            TaskType: response.Type,
            Action: response.Action,
            Status: response.Status,
            ErrorCode: response.ErrorCode,
            IdfyCreatedAt: response.CreatedAt,
            IdfyCompletedAt: response.CompletedAt);
    }

    private static (string? TaskId, string? GroupId) ReadRequest(string? body)
    {
        if (string.IsNullOrEmpty(body)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return (null, null);
            return (Str(doc.RootElement, "task_id"), Str(doc.RootElement, "group_id"));
        }
        catch (JsonException) { return (null, null); }
    }

    private static ResponseFields ReadResponse(string? body)
    {
        if (string.IsNullOrEmpty(body)) return default;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // Driving-license and passport wrap the task object in a single-element array.
            if (root.ValueKind == JsonValueKind.Array)
                root = root.GetArrayLength() > 0 ? root[0] : default;
            if (root.ValueKind != JsonValueKind.Object) return default;

            return new ResponseFields
            {
                TaskId = Str(root, "task_id"),
                GroupId = Str(root, "group_id"),
                RequestId = Str(root, "request_id"),
                Type = Str(root, "type"),
                Action = Str(root, "action"),
                Status = Str(root, "status"),
                // Present on error bodies.
                ErrorCode = Str(root, "error") ?? Str(root, "error_code") ?? Str(root, "code"),
                CreatedAt = Date(root, "created_at"),
                CompletedAt = Date(root, "completed_at"),
            };
        }
        catch (JsonException) { return default; }
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTimeOffset? Date(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTimeOffset(out var d)
            ? d
            : null;

    private struct ResponseFields
    {
        public string? TaskId, GroupId, RequestId, Type, Action, Status, ErrorCode;
        public DateTimeOffset? CreatedAt, CompletedAt;
    }
}
