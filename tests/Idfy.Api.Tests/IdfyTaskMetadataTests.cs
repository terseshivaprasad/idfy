using Idfy.Api.Logging;

namespace Idfy.Api.Tests;

public class IdfyTaskMetadataTests
{
    [Fact]
    public void Parses_successful_extraction_envelope()
    {
        var request = """{"task_id":"t-req","group_id":"g-req","data":{"document1":"https://x/p.jpg"}}""";
        var response = """
            {"action":"extract","type":"ind_pan","status":"completed","task_id":"t-resp",
             "group_id":"g-resp","request_id":"r1","created_at":"2024-07-10T12:28:26+05:30",
             "completed_at":"2024-07-10T12:28:27+05:30","result":{"extraction_output":{"id_number":"X"}}}
            """;

        var m = IdfyTaskMetadata.Parse(request, response);

        Assert.Equal("t-resp", m.TaskId);   // response id wins over request id
        Assert.Equal("g-resp", m.GroupId);
        Assert.Equal("r1", m.RequestId);
        Assert.Equal("ind_pan", m.TaskType);
        Assert.Equal("extract", m.Action);
        Assert.Equal("completed", m.Status);
        Assert.Null(m.ErrorCode);
        Assert.NotNull(m.IdfyCreatedAt);
        Assert.NotNull(m.IdfyCompletedAt);
    }

    [Fact]
    public void Unwraps_array_wrapped_response()
    {
        var response = """[{"action":"extract","type":"ind_driving_license","status":"completed","request_id":"r2"}]""";
        var m = IdfyTaskMetadata.Parse(null, response);

        Assert.Equal("ind_driving_license", m.TaskType);
        Assert.Equal("completed", m.Status);
        Assert.Equal("r2", m.RequestId);
    }

    [Fact]
    public void Reads_error_code_from_failure_body()
    {
        var m = IdfyTaskMetadata.Parse(
            """{"task_id":"t1","group_id":"g1","data":{}}""",
            """{"error":"INVALID_IMAGE","message":"Multiple documents detected","request_id":"r3"}""");

        Assert.Equal("INVALID_IMAGE", m.ErrorCode);
        Assert.Equal("r3", m.RequestId);
        Assert.Null(m.TaskType);
        Assert.Null(m.Status);
        // Falls back to request ids when the error body has none.
        Assert.Equal("t1", m.TaskId);
        Assert.Equal("g1", m.GroupId);
    }

    [Fact]
    public void Handles_missing_response()
    {
        var m = IdfyTaskMetadata.Parse("""{"task_id":"t1","group_id":"g1","data":{}}""", null);
        Assert.Equal("t1", m.TaskId);
        Assert.Null(m.TaskType);
        Assert.Null(m.RequestId);
    }

    [Fact]
    public void Handles_non_json_response()
    {
        var m = IdfyTaskMetadata.Parse(null, "<html>Bad Gateway</html>");
        Assert.Null(m.TaskType);
        Assert.Null(m.Status);
        Assert.Null(m.ErrorCode);
    }
}
