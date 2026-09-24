using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Idfy.Api.Data;
using Idfy.Api.Endpoints;
using Idfy.Api.Logging;
using Idfy.Api.Options;
using Idfy.Api.Security;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Give hosted services (the log writer especially) time to drain on shutdown / IIS app-pool
// recycle. Keep this below the IIS ANCM shutdownTimeLimit in web.config so the drain isn't cut off.
builder.Services.Configure<HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("ShutdownTimeoutSeconds", 25)));

// Cap the request body up front (base64 images are checked at 3MB; multipart uploads at 10MB).
builder.WebHost.ConfigureKestrel(o =>
    o.Limits.MaxRequestBodySize = builder.Configuration.GetValue("MaxRequestBodyBytes", 11 * 1024 * 1024));

builder.Services.AddOptions<IdfyOptions>()
    .Bind(builder.Configuration.GetSection(IdfyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var logDbConnection = builder.Configuration.GetConnectionString("LogDb");
if (string.IsNullOrWhiteSpace(logDbConnection))
    throw new InvalidOperationException("Connection string 'LogDb' is not configured.");
builder.Services.AddSingleton(new LogRepository(logDbConnection));
builder.Services.AddSingleton<DbLogQueue>();
builder.Services.AddHostedService<DbLogWriter>();
builder.Services.AddTransient<IdfyLoggingHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOptions<LogRetentionOptions>()
    .Bind(builder.Configuration.GetSection(LogRetentionOptions.SectionName));
builder.Services.AddHostedService<LogRetentionService>();

builder.Services.AddHealthChecks()
    .AddCheck<LogDbHealthCheck>("logdb");

var idfyTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Idfy:TimeoutSeconds", 60));

builder.Services.AddHttpClient<IIdfyClient, IdfyClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<IdfyOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline owns timeouts
    http.DefaultRequestHeaders.Add("account-id", options.AccountId);
    http.DefaultRequestHeaders.Add("api-key", options.ApiKey);
})
.AddHttpMessageHandler<IdfyLoggingHandler>()
.AddStandardResilienceHandler(o =>
{
    // IDfy sync tasks can take up to ~50s, so timeouts are generous.
    o.AttemptTimeout.Timeout = idfyTimeout;
    o.TotalRequestTimeout.Timeout = idfyTimeout + TimeSpan.FromSeconds(5);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(idfyTimeout.TotalSeconds * 2 + 10);
    // Never retry: re-POSTing an extraction is not safely idempotent and could double-charge credits.
    o.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
});

// API-key authentication: callers must send a valid key; every endpoint requires it
// (fallback policy) except health and, in Development, the OpenAPI document.
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyOptions.SectionName))
    .Validate(o => o.Keys.Count > 0, "At least one ApiAuth:Keys value must be configured.")
    .ValidateOnStart();

builder.Services.AddSingleton<ApiKeyValidator>();
builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // One fixed window per caller (by API key, falling back to remote IP).
    limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var config = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var apiKeyHeader = context.RequestServices.GetRequiredService<IOptions<ApiKeyOptions>>().Value.HeaderName;
        var partition = context.Request.Headers[apiKeyHeader].FirstOrDefault()
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.PermitPerWindow,
            Window = TimeSpan.FromSeconds(config.WindowSeconds),
            QueueLimit = 0,
        });
    });
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    // Inbound request binding only (does not affect IDfy response parsing, which may carry extra fields).
    o.SerializerOptions.AllowDuplicateProperties = false;              // duplicate-parameter detection
    o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow; // reject unknown params
});

// Trust the forwarded client IP/proto only when explicitly enabled (i.e. behind a known proxy),
// so an attacker cannot spoof X-Forwarded-For when the app is exposed directly.
var forwardedHeadersEnabled = builder.Configuration.GetValue("ForwardedHeaders:Enabled", false);
if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (forwardedHeadersEnabled)
    app.UseForwardedHeaders();

app.UseResponseCompression();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapDocumentEndpoints();
app.MapPanEndpoints();
app.MapAadhaarEndpoints();
app.MapDrivingLicenseEndpoints();
app.MapPassportEndpoints();
app.MapVoterIdEndpoints();

app.Run();

public partial class Program;
