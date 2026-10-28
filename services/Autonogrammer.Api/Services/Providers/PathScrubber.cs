using System.Text.RegularExpressions;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>Makes text that came from omp fit for a response: an absolute Unix path in it is replaced by a placeholder.</summary>
internal static partial class PathScrubber
{
    /// <summary>The text a path is replaced by.</summary>
    public const string Placeholder = "<path>";

    /// <summary>Replaces every absolute Unix path in the text. A slash inside a word or a URL is not a path and stays.</summary>
    /// <param name="text">The text from omp.</param>
    /// <returns>The text without paths.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static string WithoutPaths(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return AbsolutePath().Replace(text, Placeholder);
    }

    [GeneratedRegex(@"(?<![\w:/.~-])/[^\s'""<>|()\[\]{},;]*[\w-]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AbsolutePath();
}
