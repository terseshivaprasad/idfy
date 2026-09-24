using System.Text.Json;
using Idfy.Api.Logging;

namespace Idfy.Api.Tests;

public class LogRedactionTests
{
    private static string? Field(string json, params string[] path)
    {
        var el = JsonDocument.Parse(json).RootElement;
        if (el.ValueKind == JsonValueKind.Array) el = el[0];
        foreach (var p in path) el = el.GetProperty(p);
        return el.ValueKind == JsonValueKind.Null ? null : el.GetString();
    }

    [Fact]
    public void MaskResponse_masks_pan_pii_and_keeps_metadata()
    {
        var body = """
            {"type":"ind_pan","status":"completed","result":{"extraction_output":{
              "id_number":"ABCDE1234F","name_on_card":"ABC DEF","fathers_name":"X Y",
              "date_of_birth":"1982-04-03","pan_type":"Individual","is_scanned":false}}}
            """;

        var masked = LogRedaction.MaskResponse(body);

        Assert.Equal("ABCD*****F", Field(masked, "result", "extraction_output", "id_number"));
        Assert.Equal("[redacted]", Field(masked, "result", "extraction_output", "name_on_card"));
        Assert.Equal("[redacted]", Field(masked, "result", "extraction_output", "fathers_name"));
        Assert.Equal("[redacted]", Field(masked, "result", "extraction_output", "date_of_birth"));
        // Non-PII is preserved.
        Assert.Equal("Individual", Field(masked, "result", "extraction_output", "pan_type"));
    }

    [Fact]
    public void MaskResponse_masks_aadhaar_number_and_address()
    {
        var body = """
            {"type":"ind_aadhaar","result":{"extraction_output":{
              "id_number":"123412341234","address":"12 Main St","gender":"Female","state":"Kerala"}}}
            """;

        var masked = LogRedaction.MaskResponse(body);

        Assert.Equal("1234*******4", Field(masked, "result", "extraction_output", "id_number"));
        Assert.Equal("[redacted]", Field(masked, "result", "extraction_output", "address"));
        Assert.Equal("[redacted]", Field(masked, "result", "extraction_output", "gender"));
        Assert.Equal("[redacted]", Field(masked, "result", "extraction_output", "state"));
    }

    [Fact]
    public void MaskResponse_handles_array_wrapped_response()
    {
        var body = """[{"type":"ind_driving_license","result":{"extraction_output":{"id_number":"DL0420110149646","name_on_card":"ABC"}}}]""";

        var masked = LogRedaction.MaskResponse(body);

        Assert.Equal("[redacted]", Field(masked, "result", "extraction_output", "name_on_card"));
        Assert.StartsWith("DL", Field(masked, "result", "extraction_output", "id_number"));
        Assert.Contains("*", Field(masked, "result", "extraction_output", "id_number")!);
    }

    [Fact]
    public void MaskResponse_passes_through_non_json()
    {
        const string html = "<html>Bad Gateway</html>";
        Assert.Equal(html, LogRedaction.MaskResponse(html));
    }

    [Fact]
    public void MaskResponse_leaves_empty_values_untouched()
    {
        var body = """{"result":{"extraction_output":{"date_of_issue":"","id_number":"ABCDE1234F"}}}""";
        var masked = LogRedaction.MaskResponse(body);
        Assert.Equal("", Field(masked, "result", "extraction_output", "date_of_issue"));
    }

    [Fact]
    public void RedactRequest_replaces_base64_and_extracts_ids()
    {
        var body = """{"task_id":"t1","group_id":"g1","data":{"document1":"aGVsbG8gd29ybGQ="}}""";

        var (taskId, groupId, redacted) = LogRedaction.RedactRequest(body);

        Assert.Equal("t1", taskId);
        Assert.Equal("g1", groupId);
        Assert.Contains("base64 redacted", redacted);
        Assert.DoesNotContain("aGVsbG8", redacted);
    }

    [Fact]
    public void RedactRequest_masks_pii_in_verify_request()
    {
        var body = """{"task_id":"t1","group_id":"g1","data":{"id_number":"BR0120150052869","date_of_birth":"1985-02-15"}}""";

        var (taskId, _, redacted) = LogRedaction.RedactRequest(body);

        Assert.Equal("t1", taskId);
        Assert.DoesNotContain("BR0120150052869", redacted);   // id masked
        Assert.DoesNotContain("1985-02-15", redacted);        // dob redacted
        Assert.Contains("[redacted]", redacted);
    }

    [Fact]
    public void MaskResponse_masks_verify_source_output()
    {
        var body = """
            [{"type":"ind_driving_license","result":{"source_output":{
              "name":"ABC DEF","dob":"1985-02-15","city":"Patna","relatives_name":"XYZ",
              "id_number":"BR-****52869","card_serial_no":"SER12345678","dl_status":"Active"}}}]
            """;

        var masked = LogRedaction.MaskResponse(body);

        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "name"));
        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "dob"));
        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "city"));
        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "relatives_name"));
        Assert.Equal("Active", Field(masked, "result", "source_output", "dl_status")); // status kept
    }

    [Fact]
    public void MaskResponse_masks_voter_id_source_output()
    {
        var body = """
            {"type":"ind_voter_id","result":{"match_output":{"name_on_card":1},"source_output":{
              "name_on_card":"shubh pipalia","rln_name":"chetan pipalia","house_no":"12",
              "gender":"M","district":"Mumbai Suburban","id_number":"ABC1234567",
              "ac_no":"160","ps_name":"Govind Nagar School","status":"id_found"}}}
            """;

        var masked = LogRedaction.MaskResponse(body);

        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "name_on_card"));
        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "rln_name"));
        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "house_no"));
        // Electoral-roll admin fields and match score kept.
        Assert.Equal("160", Field(masked, "result", "source_output", "ac_no"));
        Assert.Equal("id_found", Field(masked, "result", "source_output", "status"));
    }

    [Fact]
    public void MaskResponse_redacts_passport_status_free_text()
    {
        var body = """
            {"type":"ind_passport","result":{"source_output":{
              "name":"John","surname":"Doe","file_number":"xyz","application_date":"2012-01-01",
              "passport_status":"Passport K1234567 dispatched, Tracking EN12345654321N.","status":"id_found"}}}
            """;

        var masked = LogRedaction.MaskResponse(body);

        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "name"));
        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "surname"));
        // The free-text status embeds the passport & tracking number, so the whole field goes.
        Assert.Equal("[redacted]", Field(masked, "result", "source_output", "passport_status"));
        Assert.DoesNotContain("K1234567", masked);
        Assert.Equal("id_found", Field(masked, "result", "source_output", "status"));
        Assert.Equal("2012-01-01", Field(masked, "result", "source_output", "application_date"));
    }

    [Fact]
    public void RedactRequest_keeps_url_documents()
    {
        var body = """{"task_id":"t1","group_id":"g1","data":{"document1":"https://example.com/x.jpg"}}""";
        var (_, _, redacted) = LogRedaction.RedactRequest(body);
        Assert.Contains("https://example.com/x.jpg", redacted);
    }
}
