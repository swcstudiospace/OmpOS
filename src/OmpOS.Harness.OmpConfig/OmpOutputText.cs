using System.Text;

namespace OmpOS.Harness.OmpConfig;

/// <summary>Turns what omp wrote to its standard error into short, path-free text safe to show in a settings page.</summary>
internal static class OmpOutputText
{
    /// <summary>The marker omp 18.12.6 writes when <c>models.yml</c> fails validation. The command still exits 0.</summary>
    public const string ValidationFailureMarker = "validation failed";

    private const int MaxLineCharacters = 400;

    /// <summary>Returns the first non-empty line of <paramref name="text"/>, or null when there is none.</summary>
    /// <param name="text">The standard error text.</param>
    /// <param name="options">The options that name the home and agent directories to hide.</param>
    /// <returns>The cleaned line, or null.</returns>
    public static string? FirstLine(string text, OmpConfigOptions options) => Lines(text, options, 1);

    /// <summary>
    /// Returns up to the first two non-empty lines joined by a space, which for a <c>models.yml</c> failure is the warning and the
    /// validation error that names the offending field.
    /// </summary>
    /// <param name="text">The standard error text.</param>
    /// <param name="options">The options that name the home and agent directories to hide.</param>
    /// <returns>The cleaned text, or null when there is none.</returns>
    public static string? FirstLines(string text, OmpConfigOptions options) => Lines(text, options, 2);

    /// <summary>Tells whether omp reported that <c>models.yml</c> failed validation.</summary>
    /// <param name="standardError">The standard error text.</param>
    /// <returns>True when the validation failure marker is present.</returns>
    public static bool ReportsValidationFailure(string standardError) =>
        standardError.Contains(ValidationFailureMarker, StringComparison.OrdinalIgnoreCase);

    private static string? Lines(string text, OmpConfigOptions options, int count)
    {
        var taken = new List<string>(count);
        foreach (var raw in text.Split('\n'))
        {
            var line = Clean(raw, options);
            if (line.Length == 0)
            {
                continue;
            }

            taken.Add(line);
            if (taken.Count == count)
            {
                break;
            }
        }

        return taken.Count == 0 ? null : string.Join(' ', taken);
    }

    private static string Clean(string raw, OmpConfigOptions options)
    {
        var line = raw.Trim();
        line = Hide(line, options.ResolveAgentDirectory(), "<agent dir>");
        line = Hide(line, options.ResolveHome(), "~");
        var builder = new StringBuilder(line.Length);
        foreach (var character in line)
        {
            if (!char.IsControl(character))
            {
                _ = builder.Append(character);
            }
        }

        return builder.Length > MaxLineCharacters ? builder.ToString(0, MaxLineCharacters) : builder.ToString();
    }

    private static string Hide(string line, string path, string replacement) =>
        path.Length >= 2 ? line.Replace(path, replacement, StringComparison.Ordinal) : line;
}
