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

    public IdfyMaskAadhaarData? LastMaskData { get; private set; }

    public Task<IdfyTaskResponse<MaskResult>> MaskAadhaarAsync(
        IdfyTaskRequest<IdfyMaskAadhaarData> r, CancellationToken ct = default)
    {
        LastMaskData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<MaskResult> { Status = "completed", Type = "ind_aadhaar" }));
    }

    public IdfyFaceCompareData? LastFaceData { get; private set; }

    public Task<IdfyTaskResponse<FaceCompareResult>> CompareFacesAsync(
        IdfyTaskRequest<IdfyFaceCompareData> r, CancellationToken ct = default)
    {
        LastFaceData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<FaceCompareResult> { Status = "completed", Type = "face" }));
    }

    public Task<IdfyTaskResponse<DrivingLicenseResult>> ExtractDrivingLicenseAsync(
        IdfyTaskRequest<IdfyDocumentData> r, CancellationToken ct = default) =>
        Task.FromResult(Result(new IdfyTaskResponse<DrivingLicenseResult> { Status = "completed", Type = "ind_driving_license" }));

    public Task<IdfyTaskResponse<PassportResult>> ExtractPassportAsync(
        IdfyTaskRequest<IdfyPassportData> r, CancellationToken ct = default)
    {
        LastPassportData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<PassportResult> { Status = "completed", Type = "ind_passport" }));
    }

    public IdfyVoterIdData? LastVoterIdData { get; private set; }

    public Task<IdfyTaskResponse<VoterIdResult>> ExtractVoterIdAsync(
        IdfyTaskRequest<IdfyVoterIdData> r, CancellationToken ct = default)
    {
        LastVoterIdData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<VoterIdResult> { Status = "completed", Type = "ind_voter_id" }));
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

    public IdfyPanAadhaarLinkData? LastPanAadhaarData { get; private set; }

    public Task<IdfyTaskResponse<PanAadhaarLinkResult>> VerifyPanAadhaarLinkAsync(
        IdfyTaskRequest<IdfyPanAadhaarLinkData> r, CancellationToken ct = default)
    {
        LastPanAadhaarData = r.Data;
        return Task.FromResult(Result(new IdfyTaskResponse<PanAadhaarLinkResult> { Status = "completed", Type = "pan_aadhaar_link" }));
    }

    public Task<IdfyAsyncSubmitResponse> SubmitPanAadhaarLinkAsync(
        IdfyTaskRequest<IdfyPanAadhaarLinkData> r, CancellationToken ct = default)
    {
        LastPanAadhaarData = r.Data;
        return Task.FromResult(Result(new IdfyAsyncSubmitResponse { RequestId = "req-panaadhaar" }));
    }

    public Task<IdfyTaskResponse<PanAadhaarLinkResult>?> GetPanAadhaarLinkAsync(
        string requestId, CancellationToken ct = default) =>
        Task.FromResult(Result(VerifyResultReady
            ? new IdfyTaskResponse<PanAadhaarLinkResult> { Status = "completed", Type = "pan_aadhaar_link" }
            : null));
}

public sealed class IntegrationTests : IClassFixture<IntegrationTests.Factory>
{
    private readonly Factory _factory;

    public IntegrationTests(Factory factory) => _factory = factory;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public FakeIdfyClient Idfy { get; } = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
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

    private HttpClient Client() => _factory.CreateClient();

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
    public async Task Aadhaar_mask_requires_consent()
    {
        var resp = await Client().PostAsJsonAsync("/api/aadhaar/mask", new { document = "https://x/a.jpg" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Aadhaar_mask_forwards_consent_and_flags()
    {
        var resp = await Client().PostAsJsonAsync("/api/aadhaar/mask",
            new { document = "https://x/a.jpg", consent = true, advancedFeatures = new Dictionary<string, bool> { ["masker"] = true } });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("yes", _factory.Idfy.LastMaskData!.Consent);
        Assert.NotNull(_factory.Idfy.LastMaskData!.AdvancedFeatures);
        Assert.True((bool)_factory.Idfy.LastMaskData!.AdvancedFeatures!["masker"]!);
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

    [Theory]
    [InlineData("""{"document":"https://x/a.jpg","Document":"https://x/b.jpg"}""")]                          // differs only in case
    [InlineData("""{"document":"https://x/a.jpg","consent":true,"advancedFeatures":{"a":true,"a":false}}""")] // nested object
    public async Task Rejects_duplicate_json_property_variants(string body)
    {
        var resp = await Client().PostAsync("/api/aadhaar/mask", Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("\"traceId\"", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Allows_same_property_name_in_different_objects()
    {
        var resp = await Client().PostAsync("/api/aadhaar/mask",
            Json("""{"document":"https://x/a.jpg","consent":true,"advancedFeatures":{"consent":true}}"""));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Validates_upload_form_fields()
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } }, "file", "a.png" },
            { new StringContent("ind_xyz"), "docType" },
        };

        var resp = await Client().PostAsync("/api/documents/validate/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"DocType\"", body);
        Assert.Contains("\"traceId\"", body);
    }

    private static ByteArrayContent Image() =>
        new([1, 2, 3]) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } };

    [Theory]
    [InlineData("/api/documents/validate/upload")]
    [InlineData("/api/pan/extract/upload")]
    [InlineData("/api/aadhaar/extract/upload")]
    [InlineData("/api/aadhaar/mask/upload")]
    [InlineData("/api/driving-license/extract/upload")]
    [InlineData("/api/passport/extract/upload")]
    [InlineData("/api/voter-id/extract/upload")]
    [InlineData("/api/face/compare/upload")]
    public async Task Upload_without_any_known_field_is_a_400_not_a_500(string path)
    {
        using var form = new MultipartFormDataContent { { new StringContent("1"), "unrelated" } };

        var resp = await Client().PostAsync(path, form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("\"File\"", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_with_fields_but_no_file_reports_file_required()
    {
        using var form = new MultipartFormDataContent { { new StringContent("true"), "consent" } };

        var resp = await Client().PostAsync("/api/aadhaar/extract/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("\"File\"", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Face_compare_upload_requires_second_file()
    {
        using var form = new MultipartFormDataContent { { Image(), "file", "a.png" } };

        var resp = await Client().PostAsync("/api/face/compare/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("\"File2\"", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unreachable_idfy_returns_502_without_upstream_detail()
    {
        _factory.Idfy.ThrowOnCall = new HttpRequestException("Connection refused (10.20.30.40:443)");
        try
        {
            var resp = await Client().PostAsJsonAsync("/api/pan/extract", new { document = "https://x/p.jpg" });

            Assert.Equal(HttpStatusCode.BadGateway, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("UPSTREAM_ERROR", body);
            Assert.DoesNotContain("10.20.30.40", body);
            Assert.DoesNotContain("Connection refused", body);
        }
        finally
        {
            _factory.Idfy.ThrowOnCall = null;
        }
    }

    [Fact]
    public async Task Endpoint_problem_responses_carry_trace_id()
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }, "file", "a.txt" },
        };

        var resp = await Client().PostAsync("/api/pan/extract/upload", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
        Assert.Contains("\"traceId\"", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validation_problem_lists_field_errors()
    {
        var resp = await Client().PostAsync("/api/pan-aadhaar-link/verify/sync",
            Json("""{"panNumber":"ABC","aadhaarNumber":"12"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"PanNumber\"", body);
        Assert.Contains("\"AadhaarNumber\"", body);
        Assert.Contains("\"traceId\"", body);
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
    public async Task Pan_aadhaar_link_sync_returns_result()
    {
        var resp = await Client().PostAsJsonAsync("/api/pan-aadhaar-link/verify/sync",
            new { panNumber = "ABCDE1234F", aadhaarNumber = "123412341234" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("ABCDE1234F", _factory.Idfy.LastPanAadhaarData!.PanNumber);
    }

    [Fact]
    public async Task Pan_aadhaar_link_async_submit_and_poll()
    {
        var submit = await Client().PostAsJsonAsync("/api/pan-aadhaar-link/verify",
            new { panNumber = "ABCDE1234F", aadhaarNumber = "123412341234" });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var poll = await Client().GetAsync("/api/pan-aadhaar-link/verify/req-panaadhaar");
        Assert.Equal(HttpStatusCode.OK, poll.StatusCode);
    }

    [Theory]
    [InlineData("""{"panNumber":"BADPAN","aadhaarNumber":"123412341234"}""")]  // bad PAN
    [InlineData("""{"panNumber":"ABCDE1234F","aadhaarNumber":"12345"}""")]      // bad Aadhaar
    [InlineData("""{"aadhaarNumber":"123412341234"}""")]                        // missing PAN
    public async Task Pan_aadhaar_link_validates_input(string body)
    {
        var resp = await Client().PostAsync("/api/pan-aadhaar-link/verify/sync", Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Face_compare_forwards_both_images()
    {
        var resp = await Client().PostAsJsonAsync("/api/face/compare",
            new { document = "https://x/a.jpg", document2 = "https://x/b.jpg" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("https://x/a.jpg", _factory.Idfy.LastFaceData!.Document1);
        Assert.Equal("https://x/b.jpg", _factory.Idfy.LastFaceData!.Document2);
    }

    [Theory]
    [InlineData("""{"document":"https://x/a.jpg"}""")]                        // missing document2
    [InlineData("""{"document":"https://x/a.jpg","document2":"not base64!!"}""")] // bad document2
    public async Task Face_compare_requires_both_images(string body)
    {
        var resp = await Client().PostAsync("/api/face/compare", Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
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

    [Fact]
    public async Task Voter_id_extract_second_document_is_optional()
    {
        var resp = await Client().PostAsJsonAsync("/api/voter-id/extract", new { document = "https://x/front.jpg" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("https://x/front.jpg", _factory.Idfy.LastVoterIdData!.Document1);
        Assert.Null(_factory.Idfy.LastVoterIdData.Document2);
    }

    [Fact]
    public async Task Voter_id_extract_forwards_second_document_when_given()
    {
        var resp = await Client().PostAsJsonAsync("/api/voter-id/extract",
            new { document = "https://x/front.jpg", document2 = "https://x/back.jpg" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("https://x/back.jpg", _factory.Idfy.LastVoterIdData!.Document2);
    }

    [Theory]
    [InlineData("""{}""")]                                                        // missing document
    [InlineData("""{"document":"https://x/a.jpg","document2":"not base64!!"}""")] // bad document2
    public async Task Voter_id_extract_validates_input(string body)
    {
        var resp = await Client().PostAsync("/api/voter-id/extract", Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
