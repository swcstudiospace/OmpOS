namespace Autonogrammer.Web.Services;

/// <summary>
/// Resolves the API base URL from configuration with a boot-time query-string override.
/// The query parameter <c>apiBaseUrl</c> wins when present, but only when it names a loopback
/// host (local development); a non-loopback override is rejected instead of silently redirecting.
/// </summary>
internal static class ApiBaseUrlResolver
{
    /// <summary>The query-string parameter carrying the API base URL override.</summary>
    internal const string OverrideParameter = "apiBaseUrl";

    /// <summary>The configuration key carrying the API base URL.</summary>
    internal const string ConfigKey = "Api:BaseUrl";

    /// <summary>
    /// Resolves the API base URL: query override first, then configuration. A query override is
    /// honoured only when it names a loopback host; a non-loopback override throws rather than
    /// falling back to configuration.
    /// </summary>
    /// <param name="configuredBaseUrl">The <c>Api:BaseUrl</c> configuration value, if any.</param>
    /// <param name="currentUri">The current browser URI, used only for the query override.</param>
    /// <returns>The resolved absolute API base URL.</returns>
    /// <exception cref="InvalidOperationException">No base URL was configured or overridden, the value is not an absolute http(s) URL, or the query override is not loopback.</exception>
    internal static string Resolve(string? configuredBaseUrl, string? currentUri)
    {
        var overrideUrl = ExtractOverride(currentUri);
        var resolved = overrideUrl ?? configuredBaseUrl;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException(
                $"No API base URL is available. Set {ConfigKey} in wwwroot/appsettings.json " +
                $"or pass ?{OverrideParameter}=https://localhost:5001 in the page URL (loopback hosts only).");
        }

        return Validate(resolved, overrideUrl is not null);
    }

    /// <summary>
    /// Validates that a base URL is an absolute <c>http(s):</c> URL. A query override must also name
    /// a loopback host. Cleartext <c>http:</c> is accepted only for loopback origins (local development);
    /// anything else must be <c>https:</c> so a mistyped or attacker-crafted link fails fast instead of
    /// crashing on first use or silently redirecting prompts to an arbitrary origin.
    /// </summary>
    /// <param name="resolved">The candidate base URL.</param>
    /// <param name="fromOverride">Whether the value came from the query override; an override must name a loopback host.</param>
    /// <returns>The validated absolute base URL.</returns>
    /// <exception cref="InvalidOperationException">The value is not an absolute http(s) URL, a query override is not loopback, or http is not loopback.</exception>
    internal static string Validate(string resolved, bool fromOverride)
    {
        var source = fromOverride ? $"query parameter ?{OverrideParameter}" : $"configuration {ConfigKey}";
        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"API base URL from {source} must be an absolute http(s): URL, but was '{resolved}'.");
        }

        if (fromOverride && !uri.IsLoopback)
        {
            throw new InvalidOperationException(
                $"API base URL from {source} must name a loopback host (localhost, 127.0.0.1, or [::1]), but was '{resolved}'.");
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !uri.IsLoopback)
        {
            throw new InvalidOperationException(
                $"API base URL from {source} uses cleartext http: outside loopback ('{resolved}'); use https: or a loopback host.");
        }

        return resolved;
    }

    /// <summary>Extracts the <c>apiBaseUrl</c> query value from a URI, or null when absent.</summary>
    /// <param name="uri">The URI to inspect.</param>
    /// <returns>The decoded override value, or null.</returns>
    internal static string? ExtractOverride(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        var queryIndex = uri.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0 || queryIndex == uri.Length - 1)
        {
            return null;
        }

        var query = uri[(queryIndex + 1)..];
        var fragmentIndex = query.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex >= 0)
        {
            query = query[..fragmentIndex];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equalsIndex = pair.IndexOf('=', StringComparison.Ordinal);
            var name = equalsIndex < 0 ? pair : pair[..equalsIndex];
            if (string.Equals(name, OverrideParameter, StringComparison.Ordinal))
            {
                var value = equalsIndex < 0 ? string.Empty : Uri.UnescapeDataString(pair[(equalsIndex + 1)..].Replace('+', ' '));
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }
}
