using System.Text.Json;

namespace Ptu.Cli.Configuration;

/// <summary>Helpers for the availability API session cookie, stored as a raw "name=value" pair.</summary>
internal static class AuthCookie
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>True when the value looks like a cookie pair: non-empty name, '=', non-empty value.</summary>
    public static bool IsValidPair(string value)
    {
        var separator = value.IndexOf('=');
        return separator > 0 && separator < value.Length - 1;
    }

    /// <summary>The cookie name (text before the first '='), or the whole value when no '=' is present.</summary>
    public static string Name(string cookie)
    {
        var separator = cookie.IndexOf('=');
        return separator > 0 ? cookie[..separator] : cookie;
    }

    /// <summary>
    /// Best-effort decode of the cookie value (URL-encoded base64 JSON carrying username/expiry).
    /// Returns null when the value is opaque.
    /// </summary>
    public static AuthCookieMetadata? TryDecodeMetadata(string cookie)
    {
        try
        {
            var value = cookie[(cookie.IndexOf('=') + 1)..];
            var semicolon = value.IndexOf(';');
            if (semicolon >= 0)
            {
                value = value[..semicolon];
            }

            var json = Convert.FromBase64String(Uri.UnescapeDataString(value.Trim()));
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions);
            return payload is null
                ? null
                : new AuthCookieMetadata(
                    payload.Username,
                    payload.Expiry is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed class Payload
    {
        public string? Username { get; set; }

        public long? Expiry { get; set; }
    }
}

/// <summary>Metadata embedded in a decodable session cookie.</summary>
internal sealed record AuthCookieMetadata(string? Username, DateTimeOffset? ExpiresAt);
