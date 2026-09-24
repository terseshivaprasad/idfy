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
    };

    /// <summary>
    /// Pulls out task/group ids and replaces an inline Base64 document with a placeholder:
    /// it is identity-document PII and can be megabytes. URLs are kept as-is.
    /// </summary>
    public static (string? TaskId, string? GroupId, string? Body) RedactRequest(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return (null, null, body);

        try
        {
            if (JsonNode.Parse(body) is not JsonObject root)
                return (null, null, body);

            if (root["data"]?["document1"] is JsonValue doc && doc.TryGetValue<string>(out var value)
                && !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                root["data"]!["document1"] = $"[base64 redacted, {value.Length} chars]";
            }

            return (root["task_id"]?.ToString(), root["group_id"]?.ToString(), root.ToJsonString(WriteOptions));
        }
        catch (JsonException)
        {
            return (null, null, "[unparseable request body redacted]");
        }
    }

    /// <summary>Masks extracted personal details; non-JSON bodies (e.g. IDfy's HTML 502 page) are kept.</summary>
    public static string MaskResponse(string body)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return body;
        }

        return root is not null && Mask(root) ? root.ToJsonString(WriteOptions) : body;
    }

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
