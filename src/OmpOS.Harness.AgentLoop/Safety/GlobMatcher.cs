namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// The glob rules of the permission tokens. <c>*</c> matches any run of characters inside one path segment, <c>?</c> matches one
/// character, and a segment <c>**</c> matches any number of whole segments, including none. Paths use <c>/</c>. Nothing else is special.
/// </summary>
internal static class GlobMatcher
{
    /// <summary>Returns whether a pattern with no wildcard names a path (a folder pattern then also covers what is under it).</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>True when the pattern has no <c>*</c> and no <c>?</c>.</returns>
    public static bool IsLiteral(string pattern) => pattern.AsSpan().IndexOfAny('*', '?') < 0;

    /// <summary>Matches text against a wildcard where <c>*</c> is any run of characters (also across <c>.</c>, <c>/</c> and spaces) and <c>?</c> is one character.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="text">The text.</param>
    /// <param name="ignoreCase">True to ignore case.</param>
    /// <returns>True when the whole text matches.</returns>
    public static bool Wildcard(string pattern, string text, bool ignoreCase)
    {
        var p = 0;
        var t = 0;
        var star = -1;
        var resume = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                resume = t;
            }
            else if (p < pattern.Length && (pattern[p] == '?' || CharEquals(pattern[p], text[t], ignoreCase)))
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    /// <summary>Matches a normalized absolute path against a normalized pattern with <c>**</c> support.</summary>
    /// <param name="pattern">The pattern, with <c>/</c> separators.</param>
    /// <param name="path">The path, with <c>/</c> separators.</param>
    /// <param name="ignoreCase">True to ignore case.</param>
    /// <returns>True when the whole path matches.</returns>
    public static bool MatchPath(string pattern, string path, bool ignoreCase)
    {
        var patternRooted = pattern.StartsWith('/');
        if (patternRooted != path.StartsWith('/'))
        {
            return false;
        }

        var segments = Split(pattern);
        var parts = Split(path);
        var memo = new bool?[segments.Length + 1][];
        for (var row = 0; row < memo.Length; row++)
        {
            memo[row] = new bool?[parts.Length + 1];
        }

        return Match(segments, 0, parts, 0, ignoreCase, memo);
    }

    private static bool CharEquals(char left, char right, bool ignoreCase) =>
        left == right || (ignoreCase && char.ToUpperInvariant(left) == char.ToUpperInvariant(right));

    private static string[] Split(string value) => value.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static bool Match(string[] segments, int i, string[] parts, int j, bool ignoreCase, bool?[][] memo)
    {
        if (memo[i][j] is { } known)
        {
            return known;
        }

        bool result;
        if (i == segments.Length)
        {
            result = j == parts.Length;
        }
        else if (string.Equals(segments[i], "**", StringComparison.Ordinal))
        {
            result = false;
            for (var k = j; k <= parts.Length && !result; k++)
            {
                result = Match(segments, i + 1, parts, k, ignoreCase, memo);
            }
        }
        else
        {
            result = j < parts.Length && Wildcard(segments[i], parts[j], ignoreCase) && Match(segments, i + 1, parts, j + 1, ignoreCase, memo);
        }

        memo[i][j] = result;
        return result;
    }
}
