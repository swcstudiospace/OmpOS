namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// A webhook delivery as the trigger sees it: the raw body bytes (the signature covers them exactly) and the request headers.
/// </summary>
/// <param name="Body">The raw request body.</param>
/// <param name="Headers">The request headers. Names are matched without regard to case.</param>
public sealed record WebhookRequest(ReadOnlyMemory<byte> Body, IReadOnlyDictionary<string, string> Headers)
{
    /// <summary>Reads a header.</summary>
    /// <param name="name">The header name, matched without regard to case.</param>
    /// <param name="value">The value when the call succeeds.</param>
    /// <returns>True when the header is present and not empty.</returns>
    public bool TryGetHeader(string name, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        foreach (var (key, candidate) in Headers)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate))
            {
                value = candidate.Trim();
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
