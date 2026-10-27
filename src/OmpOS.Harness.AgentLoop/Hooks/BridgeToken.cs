using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The per-session bearer secret of the hook bridge: generation and constant-time comparison. A token is 32 random bytes
/// from the operating system's cryptographic random source, written as unpadded base64url.
/// </summary>
public static class BridgeToken
{
    private const int TokenByteCount = 32;

    /// <summary>Generates a fresh random token.</summary>
    /// <returns>A URL-safe text value with 256 bits of randomness, 43 characters long.</returns>
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteCount));

    /// <summary>
    /// Compares a presented token with the expected one without leaking where they differ or how long the expected one is.
    /// Both values are reduced to a SHA-256 digest of their UTF-8 bytes first, so the compared inputs always have the same
    /// length, and the digests are compared with <c>CryptographicOperations.FixedTimeEquals</c>. The method never
    /// throws.
    /// </summary>
    /// <param name="expected">The token of the bridge. An empty or null expected token matches nothing.</param>
    /// <param name="presented">The token the client presented, or null when it presented none.</param>
    /// <returns>True only when both are non-empty and equal.</returns>
    public static bool Matches(string expected, string? presented)
    {
        Span<byte> expectedDigest = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> presentedDigest = stackalloc byte[SHA256.HashSizeInBytes];
        Hash(expected, expectedDigest);
        Hash(presented, presentedDigest);
        var equal = CryptographicOperations.FixedTimeEquals(expectedDigest, presentedDigest);
        return equal & !string.IsNullOrEmpty(expected) & !string.IsNullOrEmpty(presented);
    }

    private static void Hash(string? text, Span<byte> destination)
    {
        if (string.IsNullOrEmpty(text))
        {
            SHA256.HashData(ReadOnlySpan<byte>.Empty, destination);
            return;
        }

        var rented = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(text.Length));
        try
        {
            var written = Encoding.UTF8.GetBytes(text, rented);
            SHA256.HashData(rented.AsSpan(0, written), destination);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }
}
