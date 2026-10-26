namespace OmpOS.Presentation.Settings;

/// <summary>
/// Reduces whatever a source reports as an endpoint to <c>host</c> or <c>host:port</c>, so a page can never show a scheme, a path,
/// a query, a fragment or credentials, even when a source hands over a full URL. Anything that is not a plain host with an
/// optional numeric port is dropped rather than shown.
/// </summary>
internal static class EndpointDisplay
{
    private const int MaxPortDigits = 5;

    /// <summary>Reduces an endpoint text to its host and port.</summary>
    /// <param name="endpoint">The text a source reported. May be a bare host, a host and port, or a full URL with credentials.</param>
    /// <returns>The host, with its port when one is given; null when nothing safe to show remains.</returns>
    internal static string? HostAndPort(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var text = endpoint.Trim();
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            text = text[(scheme + 3)..];
        }

        var end = text.IndexOfAny(['/', '\\', '?', '#']);
        if (end >= 0)
        {
            text = text[..end];
        }

        var at = text.LastIndexOf('@');
        if (at >= 0)
        {
            text = text[(at + 1)..];
        }

        return IsHostAndPort(text) ? text : null;
    }

    private static bool IsHostAndPort(string text)
    {
        string host;
        string? port;
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']', StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }

            host = text[1..close];
            var rest = text[(close + 1)..];
            if (rest.Length == 0)
            {
                port = null;
            }
            else if (rest[0] == ':')
            {
                port = rest[1..];
            }
            else
            {
                return false;
            }

            if (host.Length == 0 || !host.All(character => char.IsAsciiHexDigit(character) || character is ':' or '.'))
            {
                return false;
            }
        }
        else
        {
            var colon = text.IndexOf(':', StringComparison.Ordinal);
            host = colon < 0 ? text : text[..colon];
            port = colon < 0 ? null : text[(colon + 1)..];
            if (host.Length == 0 || !host.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_'))
            {
                return false;
            }
        }

        return port is null || (port.Length is > 0 and <= MaxPortDigits && port.All(char.IsAsciiDigit));
    }
}
