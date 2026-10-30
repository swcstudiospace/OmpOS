namespace Autonogrammer.Api.Idempotency;

/// <summary>
/// Reads the optional <c>Idempotency-Key</c> request header. A key is 1 to 128 visible ASCII characters (0x21 to 0x7E).
/// </summary>
internal static class IdempotencyKeyHeader
{
    /// <summary>Gets the header name.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Gets the maximum key length in characters.</summary>
    public const int MaxLength = 128;

    /// <summary>Reads the key from the headers.</summary>
    /// <param name="headers">The request headers.</param>
    /// <param name="key">The key when the header is present and valid; null when the header is absent.</param>
    /// <returns>True when the header is absent or valid; false when it is repeated or malformed.</returns>
    public static bool TryRead(IHeaderDictionary headers, out string? key)
    {
        ArgumentNullException.ThrowIfNull(headers);
        key = null;
        if (!headers.TryGetValue(HeaderName, out var values))
        {
            return true;
        }

        if (values.Count != 1 || values[0] is not { } value || value.Length is < 1 or > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is < '!' or > '~')
            {
                return false;
            }
        }

        key = value;
        return true;
    }
}
