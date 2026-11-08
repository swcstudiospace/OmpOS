namespace Autonogrammer.Web.Services;

/// <summary>
/// Resolves the API base URL from configuration with a boot-time query-string override.
/// The query parameter <c>apiBaseUrl</c> wins when present, but only when its origin is listed in the
/// build-time allowlist <c>Api:OverrideOrigins</c>. Any other override is refused instead of falling back
/// silently, so a crafted link cannot send prompts and the event stream to another origin.
/// </summary>
internal static class ApiBaseUrlResolver
{
    /// <summary>The query-string parameter carrying the API base URL override.</summary>
    internal const string OverrideParameter = "apiBaseUrl";

    /// <summary>The configuration key carrying the API base URL.</summary>
    internal const string ConfigKey = "Api:BaseUrl";

    /// <summary>The configuration key carrying the origins a query override may name: a build-time array of origins.</summary>
    internal const string OverrideOriginsKey = "Api:OverrideOrigins";

    /// <summary>
    /// Resolves the API base URL: query override first, then configuration. A query override is honoured only when
    /// its origin is in <paramref name="overrideOrigins"/>; any other override throws rather than falling back to
    /// configuration.
    /// </summary>
    /// <param name="configuredBaseUrl">The <c>Api:BaseUrl</c> configuration value, if any.</param>
    /// <param name="currentUri">The current browser URI, used only for the query override.</param>
    /// <param name="overrideOrigins">The <c>Api:OverrideOrigins</c> configuration values: the origins a query override may name.</param>
    /// <returns>The resolved absolute API base URL.</returns>
    /// <exception cref="InvalidOperationException">No base URL was configured or overridden, an allowlist entry is not an origin, the override is not an allowlisted origin, or the value is not an absolute http(s) URL.</exception>
    internal static string Resolve(string? configuredBaseUrl, string? currentUri, IEnumerable<string?> overrideOrigins)
    {
        var allowlist = ParseOverrideAllowlist(overrideOrigins);
        var overrideUrl = ExtractOverride(currentUri);
        if (overrideUrl is not null)
        {
            RequireAllowlisted(overrideUrl, allowlist);
        }

        var resolved = overrideUrl ?? configuredBaseUrl;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException(
                $"No API base URL is available. Set {ConfigKey} in wwwroot/appsettings.json " +
                $"or pass ?{OverrideParameter}=<origin> in the page URL for an origin listed in {OverrideOriginsKey}.");
        }

        return Validate(resolved, overrideUrl is not null);
    }

    /// <summary>
    /// Validates that a base URL is an absolute <c>http(s):</c> URL. Cleartext <c>http:</c> is accepted only for loopback
    /// origins (local development); anything else must be <c>https:</c>, so a mistyped value fails fast instead of
    /// crashing on first use.
    /// </summary>
    /// <param name="resolved">The candidate base URL.</param>
    /// <param name="fromOverride">Whether the value came from the query override; used only to name the source in the error.</param>
    /// <returns>The validated absolute base URL.</returns>
    /// <exception cref="InvalidOperationException">The value is not an absolute http(s) URL, or it is cleartext http outside loopback.</exception>
    internal static string Validate(string resolved, bool fromOverride)
    {
        var source = fromOverride ? $"query parameter ?{OverrideParameter}" : $"configuration {ConfigKey}";
        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri) || !IsHttpScheme(uri))
        {
            throw new InvalidOperationException(
                $"API base URL from {source} must be an absolute http(s): URL, but was '{resolved}'.");
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

    /// <summary>
    /// Parses the build-time override allowlist. Each entry must be an origin, <c>https://host[:port]</c> or
    /// <c>http://host:port</c>, with an optional trailing slash and nothing else; a path, query or other scheme fails fast.
    /// </summary>
    /// <param name="entries">The configured entries.</param>
    /// <returns>The allowed origins, compared case-insensitively.</returns>
    /// <exception cref="InvalidOperationException">An entry is not an http(s) origin.</exception>
    internal static IReadOnlySet<string> ParseOverrideAllowlist(IEnumerable<string?> entries)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry)
                || !Uri.TryCreate(entry, UriKind.Absolute, out var uri)
                || !IsHttpScheme(uri)
                || !string.Equals(entry.TrimEnd('/'), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{OverrideOriginsKey} entry '{entry}' must be an origin: https://host[:port], or http://host:port for loopback.");
            }

            origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }

        return origins;
    }

    private static void RequireAllowlisted(string overrideUrl, IReadOnlySet<string> allowlist)
    {
        if (!Uri.TryCreate(overrideUrl, UriKind.Absolute, out var uri)
            || !IsHttpScheme(uri)
            || !allowlist.Contains(uri.GetLeftPart(UriPartial.Authority)))
        {
            throw new InvalidOperationException(
                $"API base URL from query parameter ?{OverrideParameter} must name an origin listed in {OverrideOriginsKey}, but was '{overrideUrl}'.");
        }
    }

    private static bool IsHttpScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal);
}
