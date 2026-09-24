using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Idfy.Api.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiAuth";

    /// <summary>Header carrying the caller's key.</summary>
    public string HeaderName { get; set; } = "x-api-key";

    /// <summary>Accepted keys. When empty, the app refuses to start (fail closed).</summary>
    public HashSet<string> Keys { get; set; } = [];
}

/// <summary>
/// Validates presented keys against SHA-256 hashes of the configured keys, computed once at startup.
/// The raw keys are not retained, and matching a hash reveals nothing about the key on a timing side channel.
/// </summary>
public sealed class ApiKeyValidator
{
    private readonly HashSet<string> _hashes;

    public string HeaderName { get; }

    public ApiKeyValidator(IOptions<ApiKeyOptions> options)
    {
        var config = options.Value;
        HeaderName = config.HeaderName;
        _hashes = config.Keys.Select(Hash).ToHashSet(StringComparer.Ordinal);
    }

    public bool IsValid(string? key) => !string.IsNullOrEmpty(key) && _hashes.Contains(Hash(key));

    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}

/// <summary>Authenticates callers by a shared key in a request header.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(validator.HeaderName, out var provided) || provided.Count == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!validator.IsValid(provided.ToString()))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
