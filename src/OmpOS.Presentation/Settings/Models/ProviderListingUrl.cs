using System.Text.RegularExpressions;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// The placeholder rules the setup form of a listing shows before anything is sent: a value is one token of letters, digits, dots and
/// hyphens, and not a placeholder. The head repeats the check when it builds the <c>models.yml</c> block; this copy only lets the form
/// say what is wrong without a round trip.
/// </summary>
internal static partial class ProviderListingUrl
{
    private const int MaxValueLength = 128;

    /// <summary>Lists the placeholder names of a template in order of first appearance.</summary>
    /// <param name="template">The base URL template.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> VariablesIn(string template)
    {
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

    /// <summary>Tells whether a typed value can be part of an address.</summary>
    /// <param name="value">The value.</param>
    /// <returns>True when acceptable.</returns>
    public static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= MaxValueLength
        && ValuePattern().IsMatch(value)
        && !value.StartsWith("your", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("example", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("xxx", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fills a template; a placeholder without a value stays as it is.</summary>
    /// <param name="template">The base URL template.</param>
    /// <param name="values">The values by name.</param>
    /// <returns>The text.</returns>
    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        var text = template;
        foreach (var pair in values)
        {
            text = text.Replace("{" + pair.Key + "}", pair.Value, StringComparison.Ordinal);
        }

        return text;
    }

    [GeneratedRegex(@"\{(?<name>[a-z][a-z0-9_]*)\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.\-]*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ValuePattern();
}
