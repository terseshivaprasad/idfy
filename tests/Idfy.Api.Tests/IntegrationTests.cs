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

    public Task<IdfyTaskResponse<DrivingLicenseSourceResult>> VerifyDrivingLicenseAsync(
        IdfyTaskRequest<IdfyDrivingLicenseVerifyData> r, CancellationToken ct = default)
    {
        LastVerifyData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<DrivingLicenseSourceResult> { Status = "completed", Type = "ind_driving_license" }));
    }

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

    public IdfyPassportVerifyData? LastPassportVerifyData { get; private set; }

    public Task<IdfyTaskResponse<PassportSourceResult>> VerifyPassportAsync(
        IdfyTaskRequest<IdfyPassportVerifyData> r, CancellationToken ct = default)
    {
        LastPassportVerifyData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<PassportSourceResult> { Status = "completed", Type = "ind_passport" }));
    }

    public Task<IdfyAsyncSubmitResponse> SubmitPassportVerificationAsync(
        IdfyTaskRequest<IdfyPassportVerifyData> r, CancellationToken ct = default)
    {
        LastPassportVerifyData = r.Data;
        return Task.FromResult(Result(new IdfyAsyncSubmitResponse { RequestId = "req-passport" }));
    }

    public Task<IdfyTaskResponse<PassportSourceResult>?> GetPassportVerificationAsync(
        string requestId, CancellationToken ct = default) =>
        Task.FromResult(Result(VerifyResultReady
            ? new IdfyTaskResponse<PassportSourceResult> { Status = "completed", Type = "ind_passport" }
            : null));

    public IdfyVoterIdVerifyData? LastVoterData { get; private set; }

    public Task<IdfyTaskResponse<VoterIdSourceResult>> VerifyVoterIdAsync(
        IdfyTaskRequest<IdfyVoterIdVerifyData> r, CancellationToken ct = default)
    {
        LastVoterData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<VoterIdSourceResult> { Status = "completed", Type = "ind_voter_id" }));
    }

    public Task<IdfyAsyncSubmitResponse> SubmitVoterIdVerificationAsync(
        IdfyTaskRequest<IdfyVoterIdVerifyData> r, CancellationToken ct = default)
    {
        LastVoterData = r.Data;
        return Task.FromResult(Result(new IdfyAsyncSubmitResponse { RequestId = "req-voter" }));
    }

    public Task<IdfyTaskResponse<VoterIdSourceResult>?> GetVoterIdVerificationAsync(
        string requestId, CancellationToken ct = default) =>
        Task.FromResult(Result(VerifyResultReady
            ? new IdfyTaskResponse<VoterIdSourceResult> { Status = "completed", Type = "ind_voter_id" }
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
    public async Task Dl_verify_sync_returns_result_directly()
    {
        var resp = await Client().PostAsJsonAsync("/api/driving-license/verify/sync",
            new { idNumber = "BR0120150052869", dateOfBirth = "1985-02-15" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("1985-02-15", _factory.Idfy.LastVerifyData!.DateOfBirth);
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
    public async Task Voter_id_submit_returns_request_id()
    {
        var resp = await Client().PostAsJsonAsync("/api/voter-id/verify", new { idNumber = "ABC1234567" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("ABC1234567", _factory.Idfy.LastVoterData!.IdNumber);
    }

    [Fact]
    public async Task Voter_id_sync_returns_result_directly()
    {
        var resp = await Client().PostAsJsonAsync("/api/voter-id/verify/sync", new { idNumber = "ABC1234567" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("ABC1234567", _factory.Idfy.LastVoterData!.IdNumber);
    }

    [Fact]
    public async Task Voter_id_requires_id_number()
    {
        var resp = await Client().PostAsync("/api/voter-id/verify", Json("""{"idNumber":"x"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Voter_id_poll_returns_202_until_ready()
    {
        _factory.Idfy.VerifyResultReady = false;
        try
        {
            var resp = await Client().GetAsync("/api/voter-id/verify/req-voter");
            Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        }
        finally { _factory.Idfy.VerifyResultReady = true; }
    }

    [Fact]
    public async Task Passport_verify_sync_returns_result_directly()
    {
        var resp = await Client().PostAsJsonAsync("/api/passport/verify/sync",
            new { passportFileNumber = "AB1234567890", dateOfBirth = "1985-02-15" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("1985-02-15", _factory.Idfy.LastPassportVerifyData!.DateOfBirth);
    }

    [Theory]
    [InlineData("""{"passportFileNumber":"AB1234567890"}""")]         // missing dob
    [InlineData("""{"passportFileNumber":"AB1234567890","dateOfBirth":"bad"}""")] // bad dob
    [InlineData("""{"passportFileNumber":"AB","dateOfBirth":"1985-02-15"}""")] // file number too short
    public async Task Passport_verify_validates_input(string body)
    {
        var resp = await Client().PostAsync("/api/passport/verify/sync", Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Passport_verify_async_submit_and_poll()
    {
        var submit = await Client().PostAsJsonAsync("/api/passport/verify",
            new { passportFileNumber = "AB1234567890", dateOfBirth = "1985-02-15" });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var ready = await Client().GetAsync("/api/passport/verify/req-passport");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        _factory.Idfy.VerifyResultReady = false;
        try
        {
            var pending = await Client().GetAsync("/api/passport/verify/req-passport");
            Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        }
        finally { _factory.Idfy.VerifyResultReady = true; }
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
