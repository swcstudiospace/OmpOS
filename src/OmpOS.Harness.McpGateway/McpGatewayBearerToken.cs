using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OmpOS.Harness.McpGateway;

/// <summary>The per-gateway bearer secret: generation and constant-time checking of the <c>Authorization</c> header.</summary>
internal static class McpGatewayBearerToken
{
    private const string Scheme = "Bearer";

    /// <summary>Generates a fresh random secret from the operating system's cryptographic random source.</summary>
    /// <returns>A URL-safe text value with 256 bits of randomness.</returns>
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Checks an <c>Authorization</c> header value against the gateway secret without leaking where they differ.</summary>
    /// <param name="expected">The gateway secret.</param>
    /// <param name="authorizationHeader">The raw header value, or <c>null</c> when the request had none.</param>
    /// <returns><c>true</c> only for the <c>Bearer</c> scheme carrying exactly the secret.</returns>
    public static bool Matches(string expected, string? authorizationHeader)
    {
        ArgumentException.ThrowIfNullOrEmpty(expected);
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return false;
        }

        var separator = authorizationHeader.IndexOf(' ', StringComparison.Ordinal);
        if (separator <= 0
            || !authorizationHeader.AsSpan(0, separator).Equals(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = authorizationHeader.AsSpan(separator + 1).Trim().ToString();
        return FixedTimeEquals(expected, presented);
    }

    /// <summary>Builds the header value a client sends for a secret.</summary>
    /// <param name="token">The gateway secret.</param>
    /// <returns>The <c>Bearer</c> scheme, a space, then the secret.</returns>
    public static string HeaderValue(string token) => string.Create(CultureInfo.InvariantCulture, $"{Scheme} {token}");

    private static bool FixedTimeEquals(string expected, string presented)
    {
        // Hashing first makes both inputs the same length, so the comparison time does not reveal the secret's length.
        var expectedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var presentedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        return CryptographicOperations.FixedTimeEquals(expectedDigest, presentedDigest);
    }
}
