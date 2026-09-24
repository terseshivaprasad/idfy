using System.Threading.RateLimiting;
using Idfy.Api.Data;
using Idfy.Api.Endpoints;
using Idfy.Api.Logging;
using Idfy.Api.Options;
using Idfy.Api.Security;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddExceptionHandler<DbExceptionHandler>();

builder.Services.AddHttpClient<IIdfyClient, IdfyClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<IdfyOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    http.DefaultRequestHeaders.Add("account-id", options.AccountId);
    http.DefaultRequestHeaders.Add("api-key", options.ApiKey);
})
.AddHttpMessageHandler<IdfyLoggingHandler>();

// API-key authentication: callers must send a valid key; every endpoint requires it
// (fallback policy) except health and, in Development, the OpenAPI document.
builder.Services.AddOptions<ApiKeyOptions>()
    .Bind(builder.Configuration.GetSection(ApiKeyOptions.SectionName))
    .Validate(o => o.Keys.Count > 0, "At least one ApiAuth:Keys value must be configured.")
    .ValidateOnStart();

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

builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

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

app.MapDocumentEndpoints();
app.MapPanEndpoints();
app.MapAadhaarEndpoints();
app.MapDrivingLicenseEndpoints();
app.MapPassportEndpoints();

app.Run();
