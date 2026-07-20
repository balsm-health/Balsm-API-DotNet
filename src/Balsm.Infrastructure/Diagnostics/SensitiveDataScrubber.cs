using System.Text;
using System.Text.Json;

namespace Balsm.Infrastructure.Diagnostics;

/// <summary>
/// Redacts secrets and PHI/PII from bodies, headers, and query strings before
/// they reach the debug log. Even debug-mode request logging must never persist
/// patient data or credentials to disk (SC-006/SC-016). Field-name based: any
/// key in <see cref="SensitiveKeys"/> has its value replaced with
/// <see cref="Redacted"/> regardless of nesting depth.
/// </summary>
public static class SensitiveDataScrubber
{
    public const string Redacted = "***REDACTED***";

    /// <summary>
    /// Wire field names (snake_case) that carry credentials, tokens, or
    /// PHI/PII. Compared case-insensitively so camelCase/PascalCase variants
    /// are also caught.
    /// </summary>
    public static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Credentials / secrets
        "password", "current_password", "new_password", "old_password",
        "secret", "hmac_secret", "jwt", "client_secret",
        // Tokens
        "token", "access_token", "refresh_token", "id_token", "auth_code",
        "authorization", "api_key", "recovery_code",
        // OTP
        "otp", "otp_code", "code", "dev_code",
        // Hashes stored server-side
        "password_hash", "code_hash",
        // PII / PHI
        "email", "email_normalized", "phone", "phone_number",
        "date_of_birth", "dob", "national_id", "nationality",
    };

    /// <summary>
    /// HTTP header names whose values must never be logged.
    /// </summary>
    public static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie",
        "X-Api-Key", "X-Auth-Token",
    };

    /// <summary>
    /// Redacts sensitive keys in a JSON document. Non-JSON input is replaced
    /// wholesale (a form-encoded or opaque body could carry PHI in any shape,
    /// so it is never logged verbatim).
    /// </summary>
    public static string ScrubJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteScrubbed(doc.RootElement, writer);
            }
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        catch (JsonException)
        {
            return $"[non-json body redacted, {Encoding.UTF8.GetByteCount(body)} bytes]";
        }
    }

    private static void WriteScrubbed(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    if (SensitiveKeys.Contains(prop.Name))
                    {
                        writer.WriteString(prop.Name, Redacted);
                    }
                    else
                    {
                        writer.WritePropertyName(prop.Name);
                        WriteScrubbed(prop.Value, writer);
                    }
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteScrubbed(item, writer);
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    /// <summary>
    /// Redacts a query string (e.g. <c>?code=123&amp;lang=en</c>), replacing
    /// values of sensitive keys. Returns the leading '?'-prefixed string.
    /// </summary>
    public static string ScrubQuery(string queryString)
    {
        if (string.IsNullOrEmpty(queryString)) return queryString;

        var trimmed = queryString.StartsWith('?') ? queryString[1..] : queryString;
        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder("?");
        for (var i = 0; i < pairs.Length; i++)
        {
            if (i > 0) sb.Append('&');
            var eq = pairs[i].IndexOf('=');
            if (eq < 0) { sb.Append(pairs[i]); continue; }

            var key = pairs[i][..eq];
            sb.Append(key).Append('=');
            sb.Append(SensitiveKeys.Contains(Uri.UnescapeDataString(key)) ? Redacted : pairs[i][(eq + 1)..]);
        }
        return sb.ToString();
    }
}
