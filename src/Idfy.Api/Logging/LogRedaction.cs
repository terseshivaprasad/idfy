using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Idfy.Api.Logging;

/// <summary>Strips identity-document PII from IDfy request/response bodies before they are logged.</summary>
public static class LogRedaction
{
    private const string Redacted = "[redacted]";

    // Log text, never rendered as HTML: keep '+', non-ASCII etc. readable.
    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Extraction output fields that are removed or partially masked, matched at any depth.</summary>
    private static readonly Dictionary<string, Func<string, string>> SensitiveFields = new(StringComparer.Ordinal)
    {
        ["id_number"] = MaskId,
        ["name_on_card"] = _ => Redacted,
        ["fathers_name"] = _ => Redacted,
        ["date_of_birth"] = _ => Redacted,
        // Aadhaar output (also present under qr_output; matched at any depth).
        ["address"] = _ => Redacted,
        ["street_address"] = _ => Redacted,
        ["house_number"] = _ => Redacted,
        ["district"] = _ => Redacted,
        ["state"] = _ => Redacted,
        ["pincode"] = _ => Redacted,
        ["gender"] = _ => Redacted,
        ["year_of_birth"] = _ => Redacted,
        // Passport output.
        ["file_number"] = MaskId,
        ["first_name"] = _ => Redacted,
        ["last_name"] = _ => Redacted,
        ["mothers_name"] = _ => Redacted,
        ["name_of_spouse"] = _ => Redacted,
        ["place_of_birth"] = _ => Redacted,
        ["place_of_issue"] = _ => Redacted,
        ["nationality"] = _ => Redacted,
        // verify_with_source (driving licence) source_output, and the request's id_number/date_of_birth.
        ["name"] = _ => Redacted,
        ["dob"] = _ => Redacted,
        ["relatives_name"] = _ => Redacted,
        ["city"] = _ => Redacted,
        ["card_serial_no"] = MaskId,
        ["face_image"] = _ => Redacted,
        // Voter-id source_output.
        ["rln_name"] = _ => Redacted,
        ["house_no"] = _ => Redacted,
        // Passport verify: surname; the request's file number; and passport_status, whose free
        // text embeds the passport number and a tracking number.
        ["surname"] = _ => Redacted,
        ["passport_file_number"] = MaskId,
        ["passport_status"] = _ => Redacted,
        // pan_aadhaar_link request: both numbers are sensitive.
        ["pan_number"] = MaskId,
        ["aadhaar_number"] = MaskId,
        // Inbound (camelCase) request fields from our own callers.
        ["idNumber"] = MaskId,
        ["panNumber"] = MaskId,
        ["aadhaarNumber"] = MaskId,
        ["passportFileNumber"] = MaskId,
        ["dateOfBirth"] = _ => Redacted,
        // Aadhaar masking result: signed URLs point at the (masked and original) document images.
        ["document_url"] = _ => Redacted,
        ["original_document_url"] = _ => Redacted,
    };

    /// <summary>Parses a body once; returns null for empty or non-JSON content.</summary>
    public static JsonNode? TryParse(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return null;
        // JsonException: malformed JSON. ArgumentException: duplicate property names (a caller may
        // send those; the endpoint rejects them, but this logging path must not throw on them).
        try { return JsonNode.Parse(body); }
        catch (JsonException) { return null; }
        catch (ArgumentException) { return null; }
    }

    /// <summary>String entry point (parses); see the JsonNode overload for the work.</summary>
    public static (string? TaskId, string? GroupId, string? Body) RedactRequest(string? body) =>
        RedactRequest(TryParse(body), body);

    /// <summary>
    /// Pulls out task/group ids and replaces inline Base64 documents with a placeholder:
    /// it is identity-document PII and can be megabytes. URLs are kept as-is. Mutates <paramref name="parsed"/>.
    /// </summary>
    public static (string? TaskId, string? GroupId, string? Body) RedactRequest(JsonNode? parsed, string? original)
    {
        if (string.IsNullOrEmpty(original))
            return (null, null, original);
        if (parsed is null)
            return (null, null, "[unparseable request body redacted]");
        if (parsed is not JsonObject root)
            return (null, null, original);

        if (root["data"] is JsonObject data)
        {
            // document2: passport/voter-id back page, second face-compare image.
            RedactInlineDocument(data, "document1");
            RedactInlineDocument(data, "document2");
        }

        // Also mask any PII the request itself carries (e.g. id_number, date_of_birth for verify tasks).
        Mask(root);

        return (root["task_id"]?.ToString(), root["group_id"]?.ToString(), root.ToJsonString(WriteOptions));
    }

    /// <summary>
    /// Redacts an inbound request body from one of our own callers: strips inline Base64 images
    /// (<c>document</c>/<c>document2</c>) and masks camelCase PII (idNumber, dateOfBirth, …).
    /// Multipart uploads are not parsed — only a size note is kept.
    /// </summary>
    public static string? RedactInboundRequest(string? body, string? contentType)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return $"[multipart form-data upload, {body?.Length ?? 0} chars]";
        if (string.IsNullOrEmpty(body))
            return body;
        if (TryParse(body) is not { } parsed)
            return "[unparseable request body redacted]";
        if (parsed is JsonObject root)
        {
            RedactInlineDocument(root, "document");
            RedactInlineDocument(root, "document2");
        }
        Mask(parsed);
        return parsed.ToJsonString(WriteOptions);
    }

    private static void RedactInlineDocument(JsonObject root, string key)
    {
        if (root[key] is JsonValue value && value.TryGetValue<string>(out var text)
            && !text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            root[key] = $"[base64 redacted, {text.Length} chars]";
        }
    }

    /// <summary>String entry point (parses); see the JsonNode overload for the work.</summary>
    public static string MaskResponse(string body) => MaskResponse(TryParse(body), body);

    /// <summary>Masks extracted personal details; non-JSON bodies (e.g. IDfy's HTML 502 page) are kept.
    /// Mutates <paramref name="parsed"/>.</summary>
    public static string MaskResponse(JsonNode? parsed, string original) =>
        parsed is not null && Mask(parsed) ? parsed.ToJsonString(WriteOptions) : original;

    /// <returns>True if anything was masked.</returns>
    private static bool Mask(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (SensitiveFields.TryGetValue(key, out var mask)
                        && value is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0)
                    {
                        obj[key] = mask(text);
                        changed = true;
                    }
                    else if (value is not null)
                    {
                        changed |= Mask(value);
                    }
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    if (item is not null)
                        changed |= Mask(item);
                break;
        }
        return changed;
    }

    /// <summary>ABCDE1234F → ABCD*****F: enough to correlate a log row with a card, not to reuse it.</summary>
    private static string MaskId(string id) =>
        id.Length <= 5 ? new string('*', id.Length) : string.Concat(id.AsSpan(0, 4), new string('*', id.Length - 5), id.AsSpan(id.Length - 1));
}
