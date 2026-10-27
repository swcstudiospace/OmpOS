using System.Text.RegularExpressions;

namespace OmpOS.Harness.Providers;

/// <summary>
/// Fills the <c>{name}</c> placeholders of a catalogue base URL. A value is one DNS-label-like token (letters, digits, dots and
/// hyphens), so a value can never add a path, a query, user information or a second host. The result must be https; plain http is
/// accepted only for a loopback host when the caller says the row is a local runtime.
/// </summary>
public static partial class CatalogueUrlTemplate
{
    /// <summary>The longest value one placeholder accepts.</summary>
    public const int MaxValueLength = 128;

    /// <summary>Lists the placeholder names of <paramref name="template"/> in order of first appearance, without duplicates.</summary>
    /// <param name="template">The base URL template.</param>
    /// <returns>The names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> is null.</exception>
    public static IReadOnlyList<string> VariablesIn(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var names = new List<string>();
        foreach (Match match in PlaceholderPattern().Matches(template))
        {
            var name = match.Groups["name"].Value;
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>Tells whether a placeholder value is acceptable: a non-empty token of letters, digits, dots and hyphens that does not look like a placeholder.</summary>
    /// <param name="value">The value the user typed.</param>
    /// <returns>True when the value may be put into a URL.</returns>
    public static bool IsAcceptableValue(string? value) =>
        value is not null && value.Length <= MaxValueLength && ValuePattern().IsMatch(value) && !LooksLikePlaceholder(value);

    /// <summary>Fills the placeholders and checks the result.</summary>
    /// <param name="template">The base URL template.</param>
    /// <param name="values">The value of each placeholder by name. Names the template does not hold are ignored.</param>
    /// <param name="allowLoopbackHttp">True for a local runtime, which may use http on a loopback host.</param>
    /// <returns>The URL, or the problem.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> or <paramref name="values"/> is null.</exception>
    public static CatalogueUrlResolution Resolve(string template, IReadOnlyDictionary<string, string> values, bool allowLoopbackHttp)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        var resolved = template;
        foreach (var name in VariablesIn(template))
        {
            if (!values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return new CatalogueUrlResolution(null, $"Enter a value for {name}.");
            }

            var trimmed = value.Trim();
            if (!IsAcceptableValue(trimmed))
            {
                return new CatalogueUrlResolution(null, $"The value for {name} must be a real identifier of letters, digits, dots and hyphens, not a placeholder.");
            }

            resolved = resolved.Replace("{" + name + "}", trimmed, StringComparison.Ordinal);
        }

        if (resolved.Contains('{', StringComparison.Ordinal) || resolved.Contains('}', StringComparison.Ordinal))
        {
            return new CatalogueUrlResolution(null, "The base URL still contains an unresolved variable.");
        }

        if (!Uri.TryCreate(resolved, UriKind.Absolute, out var url) || url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0)
        {
            return new CatalogueUrlResolution(null, "The base URL is not a valid address without user information, query or fragment.");
        }

        var https = string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
        var loopbackHttp = allowLoopbackHttp && string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && url.IsLoopback;
        return https || loopbackHttp
            ? new CatalogueUrlResolution(url, null)
            : new CatalogueUrlResolution(null, "The base URL must use https; plain http is accepted only for a runtime on this machine.");
    }

    private static bool LooksLikePlaceholder(string value) =>
        value.StartsWith("your", StringComparison.OrdinalIgnoreCase)
        || value.Contains("example", StringComparison.OrdinalIgnoreCase)
        || value.Contains("xxx", StringComparison.OrdinalIgnoreCase)
        || value.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\{(?<name>[a-z][a-z0-9_]*)\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.\-]*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ValuePattern();
}
