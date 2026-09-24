using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Idfy.Api.Data;
using Idfy.Api.Endpoints;
using Idfy.Api.Logging;
using Idfy.Api.Options;
using Idfy.Api.Services;
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
builder.Services.AddSingleton<RequestLoggingMiddleware>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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

// CORS for browser callers. Origins are configured (Cors:AllowedOrigins); a single "*"
// entry allows any origin. With no origins configured, cross-origin browser calls are blocked.
builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));

builder.Services.AddCors(o => o.AddPolicy(CorsOptions.PolicyName, policy =>
{
    var origins = builder.Configuration.GetSection(CorsOptions.SectionName)
        .GetSection(nameof(CorsOptions.AllowedOrigins)).Get<string[]>() ?? [];

    if (origins is ["*"])
        policy.AllowAnyOrigin();
    else
        policy.WithOrigins(origins);

    policy.AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // One fixed window per caller (by remote IP).
    limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var config = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
        var partition = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

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

// Log inbound /api/* request+response (uncompressed body, final status incl. handled exceptions).
// Sits inside compression and outside the exception handler for exactly that reason.
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();
app.UseCors(CorsOptions.PolicyName);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapDocumentEndpoints();
app.MapPanEndpoints();
app.MapAadhaarEndpoints();
app.MapDrivingLicenseEndpoints();
app.MapPassportEndpoints();
app.MapVoterIdEndpoints();
app.MapPanAadhaarLinkEndpoints();
app.MapFaceEndpoints();

app.Run();

public partial class Program;
