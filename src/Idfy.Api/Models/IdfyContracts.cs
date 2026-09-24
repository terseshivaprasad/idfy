using System.Text.Json.Serialization;

namespace Idfy.Api.Models;

// Wire contracts for IDfy EVE v3 sync tasks (snake_case on the wire).

public sealed record IdfyTaskRequest<TData>(
    [property: JsonPropertyName("task_id")] string TaskId,
    [property: JsonPropertyName("group_id")] string GroupId,
    [property: JsonPropertyName("data")] TData Data);

/// <summary>data for POST /v3/tasks/sync/validate/document.</summary>
public sealed record IdfyValidateDocumentData(
    [property: JsonPropertyName("document1")] string Document1,
    [property: JsonPropertyName("doc_type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DocType,
    [property: JsonPropertyName("advanced_features"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IDictionary<string, bool>? AdvancedFeatures);

/// <summary>data for extraction tasks such as POST /v3/tasks/sync/extract/ind_pan.</summary>
public sealed record IdfyDocumentData(
    [property: JsonPropertyName("document1")] string Document1);

/// <summary>data for POST /v3/tasks/sync/extract/ind_aadhaar. Consent is "yes".</summary>
public sealed record IdfyAadhaarData(
    [property: JsonPropertyName("document1")] string Document1,
    [property: JsonPropertyName("consent")] string Consent);

public sealed record IdfyTaskResponse<TResult>
{
    [JsonPropertyName("action")] public string? Action { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("task_id")] public string? TaskId { get; init; }
    [JsonPropertyName("group_id")] public string? GroupId { get; init; }
    [JsonPropertyName("request_id")] public string? RequestId { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("completed_at")] public DateTimeOffset? CompletedAt { get; init; }
    [JsonPropertyName("result")] public TResult? Result { get; init; }
}

public sealed record DocumentValidationResult
{
    [JsonPropertyName("detected_doc_side")] public string? DetectedDocSide { get; init; }
    [JsonPropertyName("detected_doc_type")] public string? DetectedDocType { get; init; }
    [JsonPropertyName("face_details")] public FaceDetails? FaceDetails { get; init; }
    [JsonPropertyName("is_readable")] public bool? IsReadable { get; init; }
    [JsonPropertyName("is_scanned")] public bool? IsScanned { get; init; }
    [JsonPropertyName("readability")] public Readability? Readability { get; init; }
}

public sealed record FaceDetails
{
    [JsonPropertyName("detected")] public bool? Detected { get; init; }
}

public sealed record Readability
{
    [JsonPropertyName("confidence")] public int? Confidence { get; init; }
}

public sealed record PanExtractionResult
{
    [JsonPropertyName("extraction_output")] public PanExtractionOutput? ExtractionOutput { get; init; }
}

public sealed record PanExtractionOutput
{
    [JsonPropertyName("age")] public int? Age { get; init; }
    // Strings, not DateOnly: IDfy sends "" when a date is not on the card.
    [JsonPropertyName("date_of_birth")] public string? DateOfBirth { get; init; }
    [JsonPropertyName("date_of_issue")] public string? DateOfIssue { get; init; }
    [JsonPropertyName("fathers_name")] public string? FathersName { get; init; }
    [JsonPropertyName("id_number")] public string? IdNumber { get; init; }
    [JsonPropertyName("is_scanned")] public bool? IsScanned { get; init; }
    [JsonPropertyName("minor")] public bool? Minor { get; init; }
    [JsonPropertyName("name_on_card")] public string? NameOnCard { get; init; }
    [JsonPropertyName("pan_type")] public string? PanType { get; init; }
}

public sealed record AadhaarExtractionResult
{
    [JsonPropertyName("extraction_output")] public AadhaarOutput? ExtractionOutput { get; init; }
    [JsonPropertyName("qr_output")] public AadhaarOutput? QrOutput { get; init; }
}

public sealed record AadhaarOutput
{
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("date_of_birth")] public string? DateOfBirth { get; init; }
    [JsonPropertyName("district")] public string? District { get; init; }
    [JsonPropertyName("fathers_name")] public string? FathersName { get; init; }
    [JsonPropertyName("gender")] public string? Gender { get; init; }
    [JsonPropertyName("house_number")] public string? HouseNumber { get; init; }
    [JsonPropertyName("id_number")] public string? IdNumber { get; init; }
    [JsonPropertyName("is_scanned")] public bool? IsScanned { get; init; }
    [JsonPropertyName("name_on_card")] public string? NameOnCard { get; init; }
    [JsonPropertyName("pincode")] public string? Pincode { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("street_address")] public string? StreetAddress { get; init; }
    [JsonPropertyName("year_of_birth")] public string? YearOfBirth { get; init; }
}
