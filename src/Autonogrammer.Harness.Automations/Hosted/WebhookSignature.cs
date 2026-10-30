using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// HMAC-SHA256 signing and constant-time verification of webhook bodies.
/// </summary>
internal static class WebhookSignature
{
    private const int DigestHexLength = 64;

    /// <summary>Computes the signature header value, <c>sha256=</c> plus lower-case hex.</summary>
    /// <param name="secret">The signing secret.</param>
    /// <param name="signed">The bytes that are signed.</param>
    /// <returns>The header value.</returns>
    public static string Sign(string secret, ReadOnlySpan<byte> signed) =>
        WebhookHeaders.SignaturePrefix + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed));

    /// <summary>Returns the bytes the generic flavour signs: the timestamp text, a dot and the body.</summary>
    /// <param name="timestamp">The timestamp header value as sent.</param>
    /// <param name="body">The raw body.</param>
    /// <returns>The bytes to sign.</returns>
    public static byte[] GenericSignedBytes(string timestamp, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));
        return signed;
    }

    /// <summary>Checks a signature header in constant time.</summary>
    /// <param name="secret">The signing secret.</param>
    /// <param name="signed">The bytes that were signed.</param>
    /// <param name="header">The signature header value as received.</param>
    /// <returns>True when the header holds the correct signature.</returns>
    public static bool Verify(string secret, ReadOnlySpan<byte> signed, string header)
    {
        // The shape of the header is not secret, so a malformed one may be refused early; the digest comparison itself is constant time.
        if (!header.StartsWith(WebhookHeaders.SignaturePrefix, StringComparison.Ordinal)
            || header.Length != WebhookHeaders.SignaturePrefix.Length + DigestHexLength)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        Span<byte> provided = stackalloc byte[32];
        if (Convert.FromHexString(header.AsSpan(WebhookHeaders.SignaturePrefix.Length), provided, out _, out var written) != OperationStatus.Done || written != provided.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    /// <summary>Parses a timestamp header: whole Unix seconds, digits only.</summary>
    /// <param name="text">The header value.</param>
    /// <param name="value">The instant when the call succeeds.</param>
    /// <returns>True when the text is a valid timestamp.</returns>
    public static bool TryParseTimestamp(string text, out DateTimeOffset value)
    {
        value = default;
        if (text.Length is 0 or > 12 || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }
}
