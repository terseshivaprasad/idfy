using System.ComponentModel.DataAnnotations;

namespace Idfy.Api.Options;

public sealed class IdfyOptions
{
    public const string SectionName = "Idfy";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://eve.idfy.com";

    [Required]
    public string AccountId { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    public AdvancedFeatureKeys AdvancedFeatureKeys { get; set; } = new();

    /// <summary>IDfy rejects Base64 payloads over 3MB with 413.</summary>
    [Range(1, int.MaxValue)]
    public int MaxBase64Length { get; set; } = 3_000_000;

    // IDfy's per-API resolution limits (apply to both width and height).

    public ImageLimits DocumentImageLimits { get; set; } = new() { MinDimension = 150, MaxDimension = 10_000 };

    public ImageLimits PanImageLimits { get; set; } = new() { MinDimension = 150, MaxDimension = 10_000 };

    public ImageLimits AadhaarImageLimits { get; set; } = new() { MinDimension = 150, MaxDimension = 10_000 };

    public ImageLimits MaskImageLimits { get; set; } = new() { MinDimension = 150, MaxDimension = 10_000 };

    public ImageLimits DrivingLicenseImageLimits { get; set; } = new() { MinDimension = 150, MaxDimension = 10_000 };

    public ImageLimits PassportImageLimits { get; set; } = new() { MinDimension = 150, MaxDimension = 10_000 };
}

public sealed class ImageLimits
{
    public int MinDimension { get; set; }
    public int MaxDimension { get; set; }
}

/// <summary>
/// IDfy only discloses the exact <c>data.advanced_features</c> key names via the sales SPOC,
/// so they are configured here. A feature whose key is blank cannot be requested.
/// </summary>
public sealed class AdvancedFeatureKeys
{
    /// <summary>Detect front/back/both side of the document (documented as <c>d*e</c>).</summary>
    public string? DocSide { get; set; }

    /// <summary>Detect a face in the document (documented as <c>f*s</c>).</summary>
    public string? Face { get; set; }

    /// <summary>Detect whether the document is a scanned copy (documented as <c>c*n</c>).</summary>
    public string? Scanned { get; set; }
}
