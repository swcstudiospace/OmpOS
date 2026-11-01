namespace Autonogrammer.Harness.BrowserUse.Policy;

/// <summary>
/// Session-scoped local-origin grants. There is no always scope: a stored grant dies with this instance.
/// </summary>
public sealed class OriginGrants
{
    private readonly HashSet<string> _grants = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds a grant. Refuses an always decision, an ineligible host, or a missing port.</summary>
    /// <param name="grant">The user decision.</param>
    /// <param name="always">Must be false. An always answer is refused.</param>
    /// <returns><see langword="true"/> when the grant was stored.</returns>
    public bool TryAdd(LocalOriginGrant grant, bool always = false)
    {
        ArgumentNullException.ThrowIfNull(grant);
        if (always || !IsEligible(grant.Host) || grant.Port is < 1 or > 65535)
        {
            return false;
        }

        _grants.Add(Key(grant.Host, grant.Port));
        return true;
    }

    /// <summary>Returns whether this session granted <paramref name="host"/> on <paramref name="port"/>.</summary>
    /// <param name="host">The requested host.</param>
    /// <param name="port">The requested port.</param>
    /// <returns><see langword="true"/> when a session grant matches.</returns>
    public bool Contains(string host, int port)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        return port is >= 1 and <= 65535 && _grants.Contains(Key(host, port));
    }

    /// <summary>Returns whether the host is one of the three local names a user may grant.</summary>
    /// <param name="host">The host.</param>
    /// <returns><see langword="true"/> for localhost, 127.0.0.1, or [::1].</returns>
    public static bool IsEligible(string host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        var normalized = Normalize(host);
        return normalized is "localhost" or "127.0.0.1" or "::1";
    }

    private static string Key(string host, int port) => Normalize(host) + ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Normalize(string host)
    {
        var trimmed = host.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed;
    }
}
