using Idfy.Api.Data;
using Idfy.Api.Endpoints;
using Idfy.Api.Logging;
using Idfy.Api.Options;
using Idfy.Api.Services;
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

builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDocumentEndpoints();
app.MapPanEndpoints();
app.MapAadhaarEndpoints();
app.MapDrivingLicenseEndpoints();
app.MapPassportEndpoints();

app.Run();
