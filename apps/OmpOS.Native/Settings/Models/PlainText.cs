using System.Text.RegularExpressions;

namespace OmpOS.Native.Settings.Models;

/// <summary>Makes text that came from omp fit for the user: a file system path in it is replaced by a placeholder, because a path says nothing the user can act on and is not for a screen or a log.</summary>
internal static partial class PlainText
{
    /// <summary>The text a path is replaced by.</summary>
    public const string PathPlaceholder = "<path>";

    /// <summary>Replaces every absolute Unix path in the text. A slash inside a word or a URL is not a path and stays.</summary>
    /// <param name="text">The text from omp.</param>
    /// <returns>The text without paths.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static string WithoutPaths(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return AbsolutePath().Replace(text, PathPlaceholder);
    }

    [GeneratedRegex(@"(?<![\w:/.~-])/[^\s'""<>|()\[\]{},;]*[\w-]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AbsolutePath();
}
