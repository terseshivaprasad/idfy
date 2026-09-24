using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;

namespace Idfy.Api.Models;

public static class DocTypes
{
    public const string Pan = "ind_pan";
    public const string Aadhaar = "ind_aadhaar";
    public const string VoterId = "ind_voter_id";
    public const string DrivingLicense = "ind_driving_license";
    public const string Passport = "ind_passport";

    public static readonly IReadOnlySet<string> All = new HashSet<string>([Pan, Aadhaar, VoterId, DrivingLicense, Passport]);
}

/// <summary>Request accepted by this API. TaskId/GroupId are generated when omitted.</summary>
public sealed class ValidateDocumentRequest
{
    /// <summary>Publicly accessible image URL, or the image as Base64.</summary>
    [Required, UrlOrBase64]
    public string Document { get; set; } = string.Empty;

    /// <summary>Expected document type: ind_pan, ind_aadhaar, ind_voter_id, ind_driving_license or ind_passport.</summary>
    [DocType]
    public string? DocType { get; set; }

    /// <summary>Detect whether the front, back or both sides of the document were supplied.</summary>
    public bool DetectDocSide { get; set; }

    /// <summary>Detect a face in the document.</summary>
    public bool DetectFace { get; set; }

    /// <summary>Detect whether the document is a scanned copy.</summary>
    public bool DetectScanned { get; set; }

    public Guid? TaskId { get; set; }

    public Guid? GroupId { get; set; }
}

/// <summary>Request for PAN extraction. TaskId/GroupId are generated when omitted.</summary>
public sealed class ExtractPanRequest
{
    /// <summary>Publicly accessible image URL, or the image as Base64.</summary>
    [Required, UrlOrBase64]
    public string Document { get; set; } = string.Empty;

    public Guid? TaskId { get; set; }

    public Guid? GroupId { get; set; }
}

/// <summary>Request for driving-license extraction. TaskId/GroupId are generated when omitted.</summary>
public sealed class ExtractDrivingLicenseRequest
{
    /// <summary>Publicly accessible image URL, or the image as Base64.</summary>
    [Required, UrlOrBase64]
    public string Document { get; set; } = string.Empty;

    public Guid? TaskId { get; set; }

    public Guid? GroupId { get; set; }
}

/// <summary>Request for passport extraction. document2 (back page) is optional.</summary>
public sealed class ExtractPassportRequest
{
    /// <summary>Front page: publicly accessible image URL, or the image as Base64.</summary>
    [Required, UrlOrBase64]
    public string Document { get; set; } = string.Empty;

    /// <summary>Back page (optional): image URL or Base64.</summary>
    [UrlOrBase64]
    public string? Document2 { get; set; }

    public Guid? TaskId { get; set; }

    public Guid? GroupId { get; set; }
}

/// <summary>Request for async driving-license verification against the government source.</summary>
public sealed class VerifyDrivingLicenseRequest
{
    /// <summary>Driving-licence number.</summary>
    [Required]
    [StringLength(32, MinimumLength = 5)]
    public string IdNumber { get; set; } = string.Empty;

    /// <summary>Holder's date of birth (YYYY-MM-DD).</summary>
    [Required]
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Also return the issuing state as a separate field.</summary>
    public bool StateInfo { get; set; }

    /// <summary>Also return whether the holder is a minor (is_minor).</summary>
    public bool AgeInfo { get; set; }

    public Guid? TaskId { get; set; }

    public Guid? GroupId { get; set; }
}

/// <summary>Request for Aadhaar extraction. Consent is mandatory (UIDAI/DPDP).</summary>
public sealed class ExtractAadhaarRequest
{
    /// <summary>Publicly accessible image URL, or the image as Base64.</summary>
    [Required, UrlOrBase64]
    public string Document { get; set; } = string.Empty;

    /// <summary>Must be true: the Aadhaar holder has consented to this extraction. Sent to IDfy as "yes".</summary>
    [Required(ErrorMessage = "Consent is required to process an Aadhaar document.")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "Consent must be given (true) to process an Aadhaar document.")]
    public bool? Consent { get; set; }

    public Guid? TaskId { get; set; }

    public Guid? GroupId { get; set; }
}

/// <summary>Accepts an absolute http(s) URL or a valid Base64 string.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class UrlOrBase64Attribute() : ValidationAttribute("{0} must be a public http(s) URL or a Base64-encoded image.")
{
    public static bool IsUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public override bool IsValid(object? value) => value switch
    {
        null => true, // [Required] handles missing values
        string s when IsUrl(s) => Uri.TryCreate(s, UriKind.Absolute, out _),
        string s => s.Length > 0 && Base64.IsValid(s),
        _ => false,
    };
}

/// <summary>Accepts null or one of <see cref="DocTypes.All"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class DocTypeAttribute() : ValidationAttribute($"{{0}} must be one of: {string.Join(", ", DocTypes.All)}.")
{
    public override bool IsValid(object? value) => value is null || value is string s && DocTypes.All.Contains(s);
}
