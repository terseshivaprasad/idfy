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

/// <summary>
/// data for POST /v3/tasks/sync/mask/ind_aadhaar. The masking option flags sit at the data level
/// (their exact key names are provided by the IDfy SPOC), so they go through JsonExtensionData.
/// </summary>
public sealed record IdfyMaskAadhaarData(
    [property: JsonPropertyName("document1")] string Document1,
    [property: JsonPropertyName("consent")] string Consent)
{
    [JsonExtensionData]
    public IDictionary<string, object?>? AdvancedFeatures { get; init; }
}

/// <summary>data for POST /v3/tasks/sync/extract/ind_passport. document2 (back page) is optional.</summary>
public sealed record IdfyPassportData(
    [property: JsonPropertyName("document1")] string Document1,
    [property: JsonPropertyName("document2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Document2);

/// <summary>data for POST /v3/tasks/async/verify_with_source/ind_driving_license.</summary>
public sealed record IdfyDrivingLicenseVerifyData(
    [property: JsonPropertyName("id_number")] string IdNumber,
    [property: JsonPropertyName("date_of_birth")] string DateOfBirth,
    [property: JsonPropertyName("advanced_details"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IdfyDlVerifyAdvancedDetails? AdvancedDetails);

public sealed record IdfyDlVerifyAdvancedDetails(
    [property: JsonPropertyName("state_info"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? StateInfo,
    [property: JsonPropertyName("age_info"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? AgeInfo);

/// <summary>data for verify_with_source/ind_voter_id.</summary>
public sealed record IdfyVoterIdVerifyData(
    [property: JsonPropertyName("id_number")] string IdNumber);

/// <summary>data for verify_with_source/ind_passport.</summary>
public sealed record IdfyPassportVerifyData(
    [property: JsonPropertyName("passport_file_number")] string PassportFileNumber,
    [property: JsonPropertyName("date_of_birth")] string DateOfBirth);

/// <summary>data for verify_with_source/pan_aadhaar_link.</summary>
public sealed record IdfyPanAadhaarLinkData(
    [property: JsonPropertyName("pan_number")] string PanNumber,
    [property: JsonPropertyName("aadhaar_number")] string AadhaarNumber);

/// <summary>Response to an async task submission: only a request_id to poll with.</summary>
public sealed record IdfyAsyncSubmitResponse
{
    [JsonPropertyName("request_id")] public string? RequestId { get; init; }
}

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

public sealed record MaskResult
{
    /// <summary>Signed URL to the masked document image.</summary>
    [JsonPropertyName("document_url")] public string? DocumentUrl { get; init; }
    [JsonPropertyName("id_number_found")] public bool? IdNumberFound { get; init; }
    /// <summary>Signed URL to the original (unmasked) document image.</summary>
    [JsonPropertyName("original_document_url")] public string? OriginalDocumentUrl { get; init; }
    [JsonPropertyName("self_link")] public string? SelfLink { get; init; }
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

public sealed record PassportResult
{
    [JsonPropertyName("extraction_output")] public PassportOutput? ExtractionOutput { get; init; }
}

public sealed record PassportOutput
{
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("date_of_birth")] public string? DateOfBirth { get; init; }
    [JsonPropertyName("date_of_expiry")] public string? DateOfExpiry { get; init; }
    [JsonPropertyName("date_of_issue")] public string? DateOfIssue { get; init; }
    [JsonPropertyName("district")] public string? District { get; init; }
    [JsonPropertyName("fathers_name")] public string? FathersName { get; init; }
    [JsonPropertyName("file_number")] public string? FileNumber { get; init; }
    [JsonPropertyName("first_name")] public string? FirstName { get; init; }
    [JsonPropertyName("gender")] public string? Gender { get; init; }
    [JsonPropertyName("id_number")] public string? IdNumber { get; init; }
    [JsonPropertyName("is_scanned")] public bool? IsScanned { get; init; }
    [JsonPropertyName("last_name")] public string? LastName { get; init; }
    [JsonPropertyName("mothers_name")] public string? MothersName { get; init; }
    [JsonPropertyName("name_of_spouse")] public string? NameOfSpouse { get; init; }
    [JsonPropertyName("name_on_card")] public string? NameOnCard { get; init; }
    [JsonPropertyName("nationality")] public string? Nationality { get; init; }
    [JsonPropertyName("pincode")] public string? Pincode { get; init; }
    [JsonPropertyName("place_of_birth")] public string? PlaceOfBirth { get; init; }
    [JsonPropertyName("place_of_issue")] public string? PlaceOfIssue { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
}

public sealed record DrivingLicenseResult
{
    [JsonPropertyName("extraction_output")] public DrivingLicenseOutput? ExtractionOutput { get; init; }
}

public sealed record DrivingLicenseSourceResult
{
    [JsonPropertyName("source_output")] public DrivingLicenseSourceOutput? SourceOutput { get; init; }
}

public sealed record DrivingLicenseSourceOutput
{
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("badge_details")] public string? BadgeDetails { get; init; }
    [JsonPropertyName("card_serial_no")] public string? CardSerialNo { get; init; }
    [JsonPropertyName("city")] public string? City { get; init; }
    [JsonPropertyName("cov_details")] public IReadOnlyList<CovDetail>? CovDetails { get; init; }
    [JsonPropertyName("date_of_issue")] public string? DateOfIssue { get; init; }
    [JsonPropertyName("date_of_last_transaction")] public string? DateOfLastTransaction { get; init; }
    [JsonPropertyName("dl_status")] public string? DlStatus { get; init; }
    [JsonPropertyName("dob")] public string? Dob { get; init; }
    [JsonPropertyName("face_image")] public string? FaceImage { get; init; }
    [JsonPropertyName("gender")] public string? Gender { get; init; }
    [JsonPropertyName("hazardous_valid_till")] public string? HazardousValidTill { get; init; }
    [JsonPropertyName("hill_valid_till")] public string? HillValidTill { get; init; }
    [JsonPropertyName("id_number")] public string? IdNumber { get; init; }
    [JsonPropertyName("issuing_rto_name")] public string? IssuingRtoName { get; init; }
    [JsonPropertyName("last_transacted_at")] public string? LastTransactedAt { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("nt_validity_from")] public string? NtValidityFrom { get; init; }
    [JsonPropertyName("nt_validity_to")] public string? NtValidityTo { get; init; }
    [JsonPropertyName("relatives_name")] public string? RelativesName { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("t_validity_from")] public string? TValidityFrom { get; init; }
    [JsonPropertyName("t_validity_to")] public string? TValidityTo { get; init; }
    // Present only when requested via advanced_details.
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("is_minor")] public bool? IsMinor { get; init; }
}

public sealed record CovDetail
{
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("cov")] public string? Cov { get; init; }
    [JsonPropertyName("issue_date")] public string? IssueDate { get; init; }
}

public sealed record PassportSourceResult
{
    [JsonPropertyName("source_output")] public PassportSourceOutput? SourceOutput { get; init; }
}

public sealed record PanAadhaarLinkResult
{
    [JsonPropertyName("source_output")] public PanAadhaarLinkOutput? SourceOutput { get; init; }
}

public sealed record PanAadhaarLinkOutput
{
    [JsonPropertyName("is_linked")] public bool? IsLinked { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
}

public sealed record PassportSourceOutput
{
    [JsonPropertyName("application_date")] public string? ApplicationDate { get; init; }
    [JsonPropertyName("date_of_birth")] public string? DateOfBirth { get; init; }
    [JsonPropertyName("file_number")] public string? FileNumber { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("passport_status")] public string? PassportStatus { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("surname")] public string? Surname { get; init; }
}

public sealed record VoterIdSourceResult
{
    [JsonPropertyName("match_output")] public VoterIdMatchOutput? MatchOutput { get; init; }
    [JsonPropertyName("source_output")] public VoterIdSourceOutput? SourceOutput { get; init; }
}

public sealed record VoterIdMatchOutput
{
    [JsonPropertyName("name_on_card")] public int? NameOnCard { get; init; }
}

public sealed record VoterIdSourceOutput
{
    [JsonPropertyName("ac_no")] public string? AcNo { get; init; }
    [JsonPropertyName("date_of_birth")] public string? DateOfBirth { get; init; }
    [JsonPropertyName("district")] public string? District { get; init; }
    [JsonPropertyName("gender")] public string? Gender { get; init; }
    [JsonPropertyName("house_no")] public string? HouseNo { get; init; }
    [JsonPropertyName("id_number")] public string? IdNumber { get; init; }
    [JsonPropertyName("last_update")] public string? LastUpdate { get; init; }
    [JsonPropertyName("name_on_card")] public string? NameOnCard { get; init; }
    [JsonPropertyName("part_no")] public string? PartNo { get; init; }
    [JsonPropertyName("ps_lat_long")] public string? PsLatLong { get; init; }
    [JsonPropertyName("ps_name")] public string? PsName { get; init; }
    [JsonPropertyName("rln_name")] public string? RlnName { get; init; }
    [JsonPropertyName("section_no")] public string? SectionNo { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("st_code")] public string? StCode { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
}

public sealed record DrivingLicenseOutput
{
    [JsonPropertyName("address")] public string? Address { get; init; }
    [JsonPropertyName("date_of_birth")] public string? DateOfBirth { get; init; }
    [JsonPropertyName("date_of_validity")] public string? DateOfValidity { get; init; }
    [JsonPropertyName("district")] public string? District { get; init; }
    [JsonPropertyName("fathers_name")] public string? FathersName { get; init; }
    [JsonPropertyName("id_number")] public string? IdNumber { get; init; }
    [JsonPropertyName("is_scanned")] public bool? IsScanned { get; init; }
    [JsonPropertyName("issue_dates")] public IDictionary<string, string?>? IssueDates { get; init; }
    [JsonPropertyName("name_on_card")] public string? NameOnCard { get; init; }
    [JsonPropertyName("pincode")] public string? Pincode { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("street_address")] public string? StreetAddress { get; init; }
    [JsonPropertyName("type")] public IReadOnlyList<string>? Type { get; init; }
    [JsonPropertyName("validity")] public IDictionary<string, string?>? Validity { get; init; }
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
