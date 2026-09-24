namespace Idfy.Api.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "Default";

    /// <summary>
    /// Browser origins allowed to call the API. List exact origins (e.g. "https://app.internal").
    /// A single "*" entry allows any origin. Empty blocks all cross-origin browser requests.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
