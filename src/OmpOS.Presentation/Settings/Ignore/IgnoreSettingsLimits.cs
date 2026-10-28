namespace OmpOS.Presentation.Settings.Ignore;

/// <summary>The limits of the global ignore list. The page refuses what is over them and the settings file never holds more.</summary>
public static class IgnoreSettingsLimits
{
    /// <summary>The most patterns the list holds.</summary>
    public const int MaxPatterns = 500;

    /// <summary>The longest pattern, in characters. It is the longest rule the ignore matcher compiles.</summary>
    public const int MaxPatternLength = 1024;

    /// <summary>The most characters all patterns hold together, so the settings file stays far below its size cap.</summary>
    public const int MaxTotalLength = 32 * 1024;

    /// <summary>Checks one pattern on its own.</summary>
    /// <param name="pattern">The trimmed pattern.</param>
    /// <returns>The reason it is refused, or null when it is fine.</returns>
    public static string? PatternProblem(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0)
        {
            return IgnoreCopy.BlankPattern;
        }

        if (pattern.Length > MaxPatternLength)
        {
            return IgnoreCopy.PatternTooLong(MaxPatternLength);
        }

        return pattern.AsSpan().ContainsAny('\r', '\n', '\0') ? IgnoreCopy.PatternHasLineBreak : null;
    }

    /// <summary>Checks a whole list.</summary>
    /// <param name="patterns">The patterns.</param>
    /// <returns>The reason the list is refused, or null when it is fine.</returns>
    public static string? ListProblem(IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Count > MaxPatterns)
        {
            return IgnoreCopy.TooManyPatterns(MaxPatterns);
        }

        var total = 0;
        foreach (var pattern in patterns)
        {
            if (pattern is null)
            {
                return IgnoreCopy.BlankPattern;
            }

            if (PatternProblem(pattern) is { } problem)
            {
                return problem;
            }

            total += pattern.Length;
        }

        return total > MaxTotalLength ? IgnoreCopy.ListTooLong : null;
    }
}
