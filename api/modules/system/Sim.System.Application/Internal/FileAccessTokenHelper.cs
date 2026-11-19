using System.Security.Cryptography;
using System.Text;

namespace Sim.System.Application.Internal;

/// <summary>
/// HMAC-SHA256 token generation and validation for Local/Database file access URLs.
/// Token format: base64url(HMAC-SHA256(signingKey, "{key}|{expiresUnixSeconds}"))
/// </summary>
public static class FileAccessTokenHelper
{
    /// <summary>
    /// Generate an HMAC-SHA256 token for the given key and expiry.
    /// </summary>
    public static string Generate(string key, DateTimeOffset expiresAt, string signingKey)
    {
        var expiresUnix = expiresAt.ToUnixTimeSeconds();
        var payload = $"{key}|{expiresUnix}";
        var hmac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(hmac);
    }

    /// <summary>
    /// Validate an HMAC token. Returns true if the token matches the expected value.
    /// </summary>
    public static bool Validate(string key, long expiresUnixSeconds, string token, string signingKey)
    {
        var payload = $"{key}|{expiresUnixSeconds}";
        var expectedBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes(payload));
        var expected = Base64UrlEncode(expectedBytes);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(expected));
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
