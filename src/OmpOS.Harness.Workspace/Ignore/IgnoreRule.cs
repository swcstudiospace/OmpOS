using System.Globalization;

namespace OmpOS.Workspace.Ignore;

/// <summary>
/// One compiled line of an ignore file (gitignore syntax). A rule is matched against the path segments below the directory that holds the file; the matcher walks
/// the ancestors of a path and asks the rule once per ancestor.
/// </summary>
internal sealed class IgnoreRule
{
    private readonly GlobSegment[] _segments;
    private readonly bool _caseSensitive;

    private IgnoreRule(string pattern, bool isNegation, bool directoryOnly, bool anchored, GlobSegment[] segments, bool caseSensitive, string source, int line)
    {
        Pattern = pattern;
        IsNegation = isNegation;
        DirectoryOnly = directoryOnly;
        Anchored = anchored;
        _segments = segments;
        _caseSensitive = caseSensitive;
        Source = source;
        Line = line;
    }

    /// <summary>Gets the rule as written, without trailing unescaped spaces; a re-including rule keeps its leading <c>!</c>.</summary>
    public string Pattern { get; }

    /// <summary>Gets whether the rule re-includes what an earlier rule ignored.</summary>
    public bool IsNegation { get; }

    /// <summary>Gets whether the rule applies to folders only (it ended with <c>/</c>).</summary>
    public bool DirectoryOnly { get; }

    /// <summary>Gets whether the rule is tied to the directory of its file (it began with <c>/</c> or holds a <c>/</c> before its end) instead of matching names at any depth.</summary>
    public bool Anchored { get; }

    /// <summary>Gets the path of the file the rule came from, or the name of the built-in or global list.</summary>
    public string Source { get; }

    /// <summary>Gets the 1-based line of the rule in its source.</summary>
    public int Line { get; }

    /// <summary>Parses one line.</summary>
    /// <param name="line">The line without its line break.</param>
    /// <param name="lineNumber">The 1-based line number, kept for explanations.</param>
    /// <param name="source">The path or list name the line came from.</param>
    /// <param name="caseSensitive">Whether the rule is compared with case.</param>
    /// <param name="maxPatternLength">The longest rule that is compiled.</param>
    /// <param name="problem">The reason the line was skipped, or <see langword="null"/> when it was fine or silently skippable.</param>
    /// <returns>The rule, or <see langword="null"/> for a blank line, a comment or a line that was skipped (then <paramref name="problem"/> may say why).</returns>
    public static IgnoreRule? Parse(string line, int lineNumber, string source, bool caseSensitive, int maxPatternLength, out string? problem)
    {
        problem = null;
        if (line.Length == 0 || line[0] == '#')
        {
            return null;
        }

        var length = TrimmedLength(line);
        if (length == 0)
        {
            return null;
        }

        var text = line[..length];
        if (text.Length > maxPatternLength)
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"The rule is longer than {maxPatternLength} characters and was skipped.");
            return null;
        }

        var isNegation = text[0] == '!';
        var body = isNegation ? text[1..] : text;
        var directoryOnly = body.EndsWith('/');
        if (directoryOnly)
        {
            body = body[..^1];
        }

        var parts = body.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            problem = "The rule has no pattern and was skipped.";
            return null;
        }

        var anchored = body.Contains('/', StringComparison.Ordinal);
        return new IgnoreRule(text, isNegation, directoryOnly, anchored, BuildSegments(parts, anchored, caseSensitive), caseSensitive, source, lineNumber);
    }

    /// <summary>Matches the rule against a path.</summary>
    /// <param name="path">The path segments below the directory of the rule's file; the last one is the entry itself.</param>
    /// <param name="isDirectory">Whether the entry is a folder.</param>
    /// <param name="maxWork">The step budget for this one match.</param>
    /// <returns>Whether the rule matches, or <see cref="IgnoreMatchResult.CapExceeded"/> when the budget ran out.</returns>
    public IgnoreMatchResult Matches(ReadOnlySpan<string> path, bool isDirectory, int maxWork)
    {
        if ((DirectoryOnly && !isDirectory) || path.IsEmpty)
        {
            return IgnoreMatchResult.NoMatch;
        }

        var budget = maxWork;
        var matched = Anchored
            ? MatchPath(path, ref budget)
            : _segments[0].Matches(path[^1], _caseSensitive, ref budget);
        if (budget < 0)
        {
            return IgnoreMatchResult.CapExceeded;
        }

        return matched ? IgnoreMatchResult.Match : IgnoreMatchResult.NoMatch;
    }

    private static int TrimmedLength(string line)
    {
        var end = line.Length;
        while (end > 0 && line[end - 1] == ' ' && !IsEscaped(line, end - 1))
        {
            end--;
        }

        return end;
    }

    private static bool IsEscaped(string line, int index)
    {
        var backslashes = 0;
        for (var position = index - 1; position >= 0 && line[position] == '\\'; position--)
        {
            backslashes++;
        }

        return (backslashes & 1) == 1;
    }

    private static GlobSegment[] BuildSegments(string[] parts, bool anchored, bool caseSensitive)
    {
        var segments = new List<GlobSegment>(parts.Length);
        foreach (var part in parts)
        {
            var segment = IgnorePatternCompiler.Compile(!anchored && string.Equals(part, "**", StringComparison.Ordinal) ? "*" : part, caseSensitive);
            if (segment.IsDoubleStar && segments.Count > 0 && segments[^1].IsDoubleStar)
            {
                continue;
            }

            segments.Add(segment);
        }

        return [.. segments];
    }

    private bool MatchPath(ReadOnlySpan<string> path, ref int budget)
    {
        var patternIndex = 0;
        var pathIndex = 0;
        var starPattern = -1;
        var starPath = 0;
        while (pathIndex < path.Length)
        {
            if (--budget < 0)
            {
                return false;
            }

            if (patternIndex < _segments.Length && _segments[patternIndex].IsDoubleStar)
            {
                starPattern = patternIndex;
                starPath = pathIndex;
                patternIndex++;
            }
            else if (patternIndex < _segments.Length && _segments[patternIndex].Matches(path[pathIndex], _caseSensitive, ref budget))
            {
                patternIndex++;
                pathIndex++;
            }
            else if (starPattern >= 0)
            {
                patternIndex = starPattern + 1;
                starPath++;
                pathIndex = starPath;
            }
            else
            {
                return false;
            }
        }

        return patternIndex == _segments.Length;
    }
}
