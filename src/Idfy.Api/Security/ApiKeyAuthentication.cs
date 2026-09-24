using System.Security.Claims;
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

/// <summary>Authenticates callers by a shared key in a request header. Constant-time compared.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<ApiKeyOptions> apiKeyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var config = apiKeyOptions.CurrentValue;

        if (!Request.Headers.TryGetValue(config.HeaderName, out var provided) || provided.Count == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        var key = provided.ToString();
        // Compare against every configured key without short-circuiting on length/content.
        var matched = false;
        foreach (var candidate in config.Keys)
            matched |= CryptographicOperations.FixedTimeEquals(key, candidate);

        if (!matched)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity(SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

internal static class CryptographicOperations
{
    /// <summary>Length-independent constant-time string comparison over UTF-8 bytes.</summary>
    public static bool FixedTimeEquals(string a, string b)
    {
        var x = System.Text.Encoding.UTF8.GetBytes(a);
        var y = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            Pad(x, Math.Max(x.Length, y.Length)), Pad(y, Math.Max(x.Length, y.Length))) && x.Length == y.Length;
    }

    private static byte[] Pad(byte[] data, int length)
    {
        if (data.Length == length) return data;
        var padded = new byte[length];
        data.CopyTo(padded, 0);
        return padded;
    }
}
