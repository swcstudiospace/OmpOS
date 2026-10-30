using System.Globalization;
using System.Text.RegularExpressions;

namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// A parsed single-file unified diff (ADE-11): the paths from its <c>---</c> and <c>+++</c> headers and its hunks in order.
/// </summary>
/// <param name="OldPath">The path from the <c>---</c> header, or null when the diff has none.</param>
/// <param name="NewPath">The path from the <c>+++</c> header, or null when the diff has none.</param>
/// <param name="Hunks">The hunks in the order they appear.</param>
public sealed partial record UnifiedDiff(string? OldPath, string? NewPath, IReadOnlyList<DiffHunk> Hunks)
{
    /// <summary>Parses a unified diff of one file.</summary>
    /// <param name="diffText">The diff text. Lines are split on <c>'\n'</c>; a <c>'\r'</c> stays part of its line's content.</param>
    /// <returns>The parsed diff.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="diffText"/> is null.</exception>
    /// <exception cref="FormatException">
    /// The text has no hunk, a hunk header is malformed, a hunk's lines do not match its counts, or the text holds more than one file.
    /// </exception>
    public static UnifiedDiff Parse(string diffText)
    {
        ArgumentNullException.ThrowIfNull(diffText);

        var lines = SplitLines(diffText);
        var index = ParseHeaders(lines, out var oldPath, out var newPath);

        var hunks = new List<DiffHunk>();
        while (index < lines.Count)
        {
            hunks.Add(ParseHunk(lines, ref index));
        }

        if (hunks.Count == 0)
        {
            throw new FormatException("The diff has no hunks.");
        }

        return new UnifiedDiff(oldPath, newPath, hunks);
    }

    [GeneratedRegex(@"^@@ -(?<oldStart>\d+)(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex HunkHeaderPattern();

    // Everything before the first hunk is a header; only the --- and +++ paths are kept.
    private static int ParseHeaders(List<string> lines, out string? oldPath, out string? newPath)
    {
        oldPath = null;
        newPath = null;
        var index = 0;
        for (; index < lines.Count && !lines[index].StartsWith("@@", StringComparison.Ordinal); index++)
        {
            if (lines[index].StartsWith("--- ", StringComparison.Ordinal))
            {
                oldPath = HeaderPath(lines[index]["--- ".Length..]);
            }
            else if (lines[index].StartsWith("+++ ", StringComparison.Ordinal))
            {
                newPath = HeaderPath(lines[index]["+++ ".Length..]);
            }
        }

        return index;
    }

    private static DiffHunk ParseHunk(List<string> lines, ref int index)
    {
        var header = HunkHeaderPattern().Match(lines[index]);
        if (!header.Success)
        {
            throw new FormatException($"Line {index + 1} is not a hunk header.");
        }

        var oldStart = ParseNumber(header.Groups["oldStart"].Value);
        var oldCount = header.Groups["oldCount"].Success ? ParseNumber(header.Groups["oldCount"].Value) : 1;
        var newStart = ParseNumber(header.Groups["newStart"].Value);
        var newCount = header.Groups["newCount"].Success ? ParseNumber(header.Groups["newCount"].Value) : 1;
        if ((oldCount > 0 && oldStart == 0) || (newCount > 0 && newStart == 0))
        {
            throw new FormatException($"Line {index + 1} names line 0 in a range that has lines.");
        }

        index++;
        var oldMissingNewline = false;
        var newMissingNewline = false;
        var hunkLines = ReadHunkLines(lines, ref index, oldCount, newCount, ref oldMissingNewline, ref newMissingNewline);

        if (index < lines.Count && !lines[index].StartsWith("@@", StringComparison.Ordinal))
        {
            throw new FormatException($"Line {index + 1} follows a hunk but is not a hunk header. Only single-file diffs are supported.");
        }

        return new DiffHunk(oldStart, oldCount, newStart, newCount, hunkLines, !oldMissingNewline, !newMissingNewline);
    }

    // The header's counts decide where the hunk ends, so a blank line inside a hunk is an empty context line.
    private static List<DiffLine> ReadHunkLines(
        List<string> lines,
        ref int index,
        int oldCount,
        int newCount,
        ref bool oldMissingNewline,
        ref bool newMissingNewline)
    {
        var hunkLines = new List<DiffLine>();
        var oldRemaining = oldCount;
        var newRemaining = newCount;
        DiffLineKind? previous = null;
        while (oldRemaining > 0 || newRemaining > 0)
        {
            if (index >= lines.Count)
            {
                throw new FormatException("The diff ends before a hunk's line counts are met.");
            }

            var line = lines[index++];
            if (line.StartsWith('\\'))
            {
                MarkNoNewline(previous, ref oldMissingNewline, ref newMissingNewline);
                continue;
            }

            var kind = ClassifyLine(line, index);
            var fits = kind switch
            {
                DiffLineKind.Context => oldRemaining > 0 && newRemaining > 0,
                DiffLineKind.Removed => oldRemaining > 0,
                _ => newRemaining > 0,
            };
            if (!fits)
            {
                throw new FormatException($"Line {index} does not fit the counts in its hunk header.");
            }

            if (kind != DiffLineKind.Added)
            {
                oldRemaining--;
            }

            if (kind != DiffLineKind.Removed)
            {
                newRemaining--;
            }

            hunkLines.Add(new DiffLine(kind, line.Length == 0 ? string.Empty : line[1..]));
            previous = kind;
        }

        while (index < lines.Count && lines[index].StartsWith('\\'))
        {
            MarkNoNewline(previous, ref oldMissingNewline, ref newMissingNewline);
            index++;
        }

        return hunkLines;
    }

    private static DiffLineKind ClassifyLine(string line, int lineNumber)
    {
        if (line.Length == 0)
        {
            return DiffLineKind.Context;
        }

        return line[0] switch
        {
            ' ' => DiffLineKind.Context,
            '-' => DiffLineKind.Removed,
            '+' => DiffLineKind.Added,
            _ => throw new FormatException($"Line {lineNumber} has no hunk marker."),
        };
    }

    // A marker after a context line says both sides lack the line break; after a removed or added line, only that side does.
    private static void MarkNoNewline(DiffLineKind? previous, ref bool oldMissing, ref bool newMissing)
    {
        if (previous is null)
        {
            throw new FormatException("A no-newline marker has no line before it.");
        }

        if (previous != DiffLineKind.Added)
        {
            oldMissing = true;
        }

        if (previous != DiffLineKind.Removed)
        {
            newMissing = true;
        }
    }

    private static List<string> SplitLines(string diffText)
    {
        var lines = diffText.Split('\n').ToList();
        if (diffText.EndsWith('\n'))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string HeaderPath(string raw)
    {
        var tab = raw.IndexOf('\t', StringComparison.Ordinal);
        return (tab >= 0 ? raw[..tab] : raw).Trim();
    }

    private static int ParseNumber(string digits)
    {
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"'{digits}' is not a valid line number or count.");
        }

        return value;
    }
}
