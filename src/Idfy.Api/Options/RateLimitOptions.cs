namespace Idfy.Api.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>Requests allowed per window, per caller IP.</summary>
    public int PermitPerWindow { get; set; } = 100;

    public int WindowSeconds { get; set; } = 60;
}
