using System.Net;
using System.Text.Json;
using Idfy.Api.Models;
using Idfy.Api.Security;
using Idfy.Api.Services;

namespace Idfy.Api.Tests;

public class ImageDimensionsTests
{
    // 1x1 PNG and JPEG encoded once, resized conceptually via header-only parsing.
    private static byte[] Png(int w, int h)
    {
        // Minimal PNG: signature + IHDR (we only parse the header, IDAT/IEND not required by the parser).
        var bytes = new byte[24];
        ReadOnlySpan<byte> sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        sig.CopyTo(bytes);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), w);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), h);
        return bytes;
    }

    [Fact]
    public void Parses_png_dimensions()
    {
        Assert.True(ImageDimensions.TryGetSize(Png(640, 480), out var w, out var h));
        Assert.Equal(640, w);
        Assert.Equal(480, h);
    }

    [Fact]
    public void Parses_jpeg_dimensions()
    {
        // JPEG: SOI, SOF0 marker with 300x200, minimal.
        byte[] jpeg =
        [
            0xFF, 0xD8,                                     // SOI
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0xC8, 0x01, 0x2C, // SOF0 len=17, h=200, w=300
            0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
        ];
        Assert.True(ImageDimensions.TryGetSize(jpeg, out var w, out var h));
        Assert.Equal(300, w);
        Assert.Equal(200, h);
    }

    [Fact]
    public void Returns_false_for_non_image()
    {
        Assert.False(ImageDimensions.TryGetSize("not an image"u8, out _, out _));
    }
}

public class IdfyApiExceptionTests
{
    [Fact]
    public void Parses_error_code_message_and_request_id()
    {
        var ex = new IdfyApiException(HttpStatusCode.UnprocessableEntity,
            """{"error":"INVALID_URL","message":"Invalid URL","request_id":"r1"}""");

        Assert.Equal("INVALID_URL", ex.ErrorCode);
        Assert.Equal("Invalid URL", ex.ErrorMessage);
        Assert.Equal("r1", ex.RequestId);
    }

    [Fact]
    public void Non_json_body_yields_no_fields()
    {
        var ex = new IdfyApiException(HttpStatusCode.BadGateway, "<html>Bad Gateway</html>");
        Assert.Null(ex.ErrorCode);
        Assert.Null(ex.RequestId);
    }

    [Theory]
    [InlineData(400, null, true)]
    [InlineData(413, null, true)]
    [InlineData(429, null, true)]
    [InlineData(422, "INVALID_IMAGE", true)]
    [InlineData(422, "INSUFFICIENT_CREDITS", false)]
    [InlineData(401, null, false)]
    [InlineData(403, null, false)]
    [InlineData(500, "INTERNAL_ERROR", false)]
    public void Classifies_caller_vs_upstream_errors(int status, string? code, bool expectedCallerError)
    {
        var body = code is null ? "{}" : $$"""{"error":"{{code}}"}""";
        var ex = new IdfyApiException((HttpStatusCode)status, body);
        Assert.Equal(expectedCallerError, ex.IsCallerError);
    }
}

public class ApiKeyComparisonTests
{
    [Fact]
    public void Equal_keys_match()
    {
        Assert.True(CryptographicOperations.FixedTimeEquals("secret-key", "secret-key"));
    }

    [Theory]
    [InlineData("secret-key", "secret-kez")]
    [InlineData("secret-key", "secret-key-longer")]
    [InlineData("", "x")]
    public void Different_keys_do_not_match(string a, string b)
    {
        Assert.False(CryptographicOperations.FixedTimeEquals(a, b));
    }
}

public class ValidationAttributeTests
{
    [Theory]
    [InlineData("https://example.com/x.jpg", true)]
    [InlineData("http://example.com/x.jpg", true)]
    [InlineData("aGVsbG8=", true)]
    [InlineData("not base64!!", false)]
    [InlineData("", false)]
    public void UrlOrBase64_validates(string value, bool expected)
    {
        Assert.Equal(expected, new UrlOrBase64Attribute().IsValid(value));
    }

    [Theory]
    [InlineData("ind_pan", true)]
    [InlineData("ind_passport", true)]
    [InlineData("ind_xyz", false)]
    [InlineData(null, true)] // optional
    public void DocType_validates(string? value, bool expected)
    {
        Assert.Equal(expected, new DocTypeAttribute().IsValid(value));
    }
}
