using Autonogrammer.Harness.BrowserUse.Options;

namespace Autonogrammer.Harness.BrowserUse.Policy;

/// <summary>
/// Scheme and host policy over <see cref="BrowserUseOptions"/>. Open is accepted in config and skipped for a
/// dispatch turn, which is treated as <see cref="PolicyMode.PromptOnNewOrigin"/>.
/// </summary>
public static class BrowserUrlPolicy
{
    /// <summary>The mode that actually applies. Dispatch never keeps <see cref="PolicyMode.Open"/>.</summary>
    /// <param name="options">The options.</param>
    /// <param name="dispatchTurn">Whether the turn was dispatched.</param>
    /// <returns>The effective mode.</returns>
    public static PolicyMode EffectiveMode(BrowserUseOptions options, bool dispatchTurn)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (dispatchTurn && options.PolicyMode == PolicyMode.Open)
        {
            return PolicyMode.PromptOnNewOrigin;
        }

        return options.PolicyMode;
    }

    /// <summary>Returns whether a top-level URL uses an allowed scheme.</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true"/> for http, https, and about:blank.</returns>
    public static bool IsTopLevelSchemeAllowed(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsTopLevelSchemeAllowed(uri);
    }

    /// <summary>Returns whether a top-level URL uses an allowed scheme.</summary>
    /// <param name="url">The URL.</param>
    /// <returns><see langword="true"/> for http, https, and about:blank.</returns>
    public static bool IsTopLevelSchemeAllowed(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (string.Equals(url.Scheme, "about", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(url.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns whether the host may be dialed under the effective mode. Denied hosts always lose.</summary>
    /// <param name="host">The host.</param>
    /// <param name="options">The options.</param>
    /// <param name="dispatchTurn">Whether the turn was dispatched.</param>
    /// <returns><see langword="false"/> when Strict omits the host or the denylist matches.</returns>
    public static bool IsHostListed(string host, BrowserUseOptions options, bool dispatchTurn)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);
        ArgumentNullException.ThrowIfNull(options);
        if (IsDenied(host, options))
        {
            return false;
        }

        if (EffectiveMode(options, dispatchTurn) == PolicyMode.Strict)
        {
            return IsAllowed(host, options);
        }

        return true;
    }

    private static bool IsDenied(string host, BrowserUseOptions options) =>
        options.DeniedHosts.Any(denied => HostEquals(denied, host));

    private static bool IsAllowed(string host, BrowserUseOptions options) =>
        options.AllowedHosts.Any(allowed => HostEquals(allowed, host));

    private static bool HostEquals(string listed, string host) =>
        string.Equals(Normalize(listed), Normalize(host), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string host)
    {
        var trimmed = host.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }
}
