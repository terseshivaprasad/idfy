using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Idfy.Api.Models;
using Idfy.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Idfy.Api.Tests;

/// <summary>Returns canned results, or throws a configured IDfy error.</summary>
public sealed class FakeIdfyClient : IIdfyClient
{
    public Exception? ThrowOnCall { get; set; }
    public IdfyPassportData? LastPassportData { get; private set; }

    private T Result<T>(T value) => ThrowOnCall is not null ? throw ThrowOnCall : value;

    public Task<IdfyTaskResponse<DocumentValidationResult>> ValidateDocumentAsync(
        IdfyTaskRequest<IdfyValidateDocumentData> r, CancellationToken ct = default) =>
        Task.FromResult(Result(new IdfyTaskResponse<DocumentValidationResult> { Status = "completed", Type = "document" }));

    public Task<IdfyTaskResponse<PanExtractionResult>> ExtractPanAsync(
        IdfyTaskRequest<IdfyDocumentData> r, CancellationToken ct = default) =>
        Task.FromResult(Result(new IdfyTaskResponse<PanExtractionResult> { Status = "completed", Type = "ind_pan" }));

    public Task<IdfyTaskResponse<AadhaarExtractionResult>> ExtractAadhaarAsync(
        IdfyTaskRequest<IdfyAadhaarData> r, CancellationToken ct = default) =>
        Task.FromResult(Result(new IdfyTaskResponse<AadhaarExtractionResult> { Status = "completed", Type = "ind_aadhaar" }));

    public Task<IdfyTaskResponse<DrivingLicenseResult>> ExtractDrivingLicenseAsync(
        IdfyTaskRequest<IdfyDocumentData> r, CancellationToken ct = default) =>
        Task.FromResult(Result(new IdfyTaskResponse<DrivingLicenseResult> { Status = "completed", Type = "ind_driving_license" }));

    public Task<IdfyTaskResponse<PassportResult>> ExtractPassportAsync(
        IdfyTaskRequest<IdfyPassportData> r, CancellationToken ct = default)
    {
        LastPassportData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<PassportResult> { Status = "completed", Type = "ind_passport" }));
    }

    public IdfyDrivingLicenseVerifyData? LastVerifyData { get; private set; }
    public bool VerifyResultReady { get; set; } = true;

    public Task<IdfyAsyncSubmitResponse> SubmitDrivingLicenseVerificationAsync(
        IdfyTaskRequest<IdfyDrivingLicenseVerifyData> r, CancellationToken ct = default)
    {
        LastVerifyData = r.Data;
        return Task.FromResult(Result(new IdfyAsyncSubmitResponse { RequestId = "req-abc" }));
    }

    public Task<IdfyTaskResponse<DrivingLicenseSourceResult>?> GetDrivingLicenseVerificationAsync(
        string requestId, CancellationToken ct = default) =>
        Task.FromResult(Result(VerifyResultReady
            ? new IdfyTaskResponse<DrivingLicenseSourceResult> { Status = "completed", Type = "ind_driving_license" }
            : null));
}

public sealed class IntegrationTests : IClassFixture<IntegrationTests.Factory>
{
    public const string ApiKey = "test-key";
    private readonly Factory _factory;

    public IntegrationTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public FakeIdfyClient Idfy { get; } = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ApiAuth:Keys:0", ApiKey);
            builder.UseSetting("ConnectionStrings:LogDb", "Server=unused;Database=x;");
            builder.UseSetting("Idfy:AccountId", "acc");
            builder.UseSetting("Idfy:ApiKey", "key");
            builder.UseSetting("Idfy:BaseUrl", "http://localhost:59999");
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<IIdfyClient>();
                s.AddSingleton<IIdfyClient>(Idfy);
            });
        }
    }

    private HttpClient Client(bool withKey = true)
    {
        var c = _factory.CreateClient();
        if (withKey) c.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        return c;
    }

    [Fact]
    public async Task Rejects_missing_api_key()
    {
        var resp = await Client(withKey: false).PostAsJsonAsync("/api/pan/extract", new { document = "https://x/p.jpg" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        // No DB, so it reports Unhealthy (503) - but it must be reachable without a key, not 401.
        var resp = await Client(withKey: false).GetAsync("/health");
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Valid_pan_request_succeeds()
    {
        var resp = await Client().PostAsJsonAsync("/api/pan/extract", new { document = "https://x/p.jpg" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Rejects_invalid_doc_type()
    {
        var resp = await Client().PostAsJsonAsync("/api/documents/validate",
            new { document = "https://x/p.jpg", docType = "ind_xyz" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Rejects_invalid_document_string()
    {
        var resp = await Client().PostAsJsonAsync("/api/pan/extract", new { document = "not base64!!" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task Aadhaar_requires_consent(bool? consent)
    {
        var resp = await Client().PostAsJsonAsync("/api/aadhaar/extract",
            new { document = "https://x/a.jpg", consent });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Aadhaar_with_consent_succeeds()
    {
        var resp = await Client().PostAsJsonAsync("/api/aadhaar/extract",
            new { document = "https://x/a.jpg", consent = true });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Idfy_caller_error_is_passed_through()
    {
        _factory.Idfy.ThrowOnCall = new IdfyApiException(HttpStatusCode.UnprocessableEntity,
            """{"error":"INVALID_IMAGE","message":"Multiple documents detected"}""");
        try
        {
            var resp = await Client().PostAsJsonAsync("/api/pan/extract", new { document = "https://x/p.jpg" });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        }
        finally { _factory.Idfy.ThrowOnCall = null; }
    }

    [Fact]
    public async Task Idfy_credential_error_becomes_502()
    {
        _factory.Idfy.ThrowOnCall = new IdfyApiException(HttpStatusCode.Forbidden, """{"message":"bad creds"}""");
        try
        {
            var resp = await Client().PostAsJsonAsync("/api/pan/extract", new { document = "https://x/p.jpg" });
            Assert.Equal(HttpStatusCode.BadGateway, resp.StatusCode);
        }
        finally { _factory.Idfy.ThrowOnCall = null; }
    }

    private static StringContent Json(string raw) => new(raw, System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Rejects_duplicate_json_property()
    {
        var resp = await Client().PostAsync("/api/pan/extract",
            Json("""{"document":"https://x/a.jpg","document":"https://x/b.jpg"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Rejects_unknown_json_property()
    {
        var resp = await Client().PostAsync("/api/pan/extract",
            Json("""{"document":"https://x/a.jpg","documnet":"typo"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Unexpected_error_returns_500_without_leaking_details()
    {
        _factory.Idfy.ThrowOnCall = new InvalidOperationException("boom-secret-internal-detail");
        try
        {
            var resp = await Client().PostAsJsonAsync("/api/pan/extract", new { document = "https://x/p.jpg" });
            Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);

            var body = await resp.Content.ReadAsStringAsync();
            Assert.DoesNotContain("boom-secret-internal-detail", body);
            Assert.DoesNotContain("InvalidOperationException", body);
            Assert.Contains("traceId", body);
        }
        finally { _factory.Idfy.ThrowOnCall = null; }
    }

    [Fact]
    public async Task Dl_verify_submit_returns_request_id()
    {
        var resp = await Client().PostAsJsonAsync("/api/driving-license/verify",
            new { idNumber = "BR0120150052869", dateOfBirth = "1985-02-15", stateInfo = true, ageInfo = true });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(true, _factory.Idfy.LastVerifyData!.AdvancedDetails!.StateInfo);
        Assert.Equal("1985-02-15", _factory.Idfy.LastVerifyData!.DateOfBirth);
    }

    [Theory]
    [InlineData("""{"idNumber":"BR0120150052869"}""")]              // missing dob
    [InlineData("""{"idNumber":"BR0120150052869","dateOfBirth":"15-02-1985"}""")] // bad dob format
    [InlineData("""{"idNumber":"BR","dateOfBirth":"1985-02-15"}""")] // id too short
    public async Task Dl_verify_validates_input(string body)
    {
        var resp = await Client().PostAsync("/api/driving-license/verify", Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Dl_verify_poll_returns_202_until_ready()
    {
        _factory.Idfy.VerifyResultReady = false;
        try
        {
            var resp = await Client().GetAsync("/api/driving-license/verify/req-abc");
            Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        }
        finally { _factory.Idfy.VerifyResultReady = true; }
    }

    [Fact]
    public async Task Dl_verify_poll_returns_result_when_ready()
    {
        var resp = await Client().GetAsync("/api/driving-license/verify/req-abc");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Passport_second_document_is_optional()
    {
        var resp = await Client().PostAsJsonAsync("/api/passport/extract", new { document = "https://x/front.jpg" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Null(_factory.Idfy.LastPassportData!.Document2);
    }

    [Fact]
    public async Task Passport_forwards_second_document_when_given()
    {
        var resp = await Client().PostAsJsonAsync("/api/passport/extract",
            new { document = "https://x/front.jpg", document2 = "https://x/back.jpg" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("https://x/back.jpg", _factory.Idfy.LastPassportData!.Document2);
    }
}
