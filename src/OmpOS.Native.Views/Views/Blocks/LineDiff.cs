namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// A line diff of two texts for the file-edit card: the lines both texts share are context, the others are removed or added. The
/// longest common subsequence of the lines decides which are shared; texts too large for that table fall back to every old line
/// removed and every new line added between their shared first and last lines. Context is cut to <see cref="ContextLines"/> lines
/// on each side of the changes, so a one-line change in a long file shows where it is.
/// </summary>
internal static class LineDiff
{
    /// <summary>The number of unchanged lines kept before the first change and after the last one.</summary>
    internal const int ContextLines = 3;

    /// <summary>The largest table of the common-subsequence search, in cells. Beyond it the changed region is shown as a block replacement.</summary>
    private const long MaxTableCells = 1_000_000;

    /// <summary>Compares two texts line by line.</summary>
    /// <param name="oldText">The text before the edit, or null for a file the edit created: then every line is added.</param>
    /// <param name="newText">The text after the edit.</param>
    /// <returns>The lines of the diff in reading order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newText"/> is null.</exception>
    public static IReadOnlyList<DiffEntry> Compute(string? oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(newText);

        var after = SplitLines(newText);
        if (oldText is null)
        {
            return after.Select(static (line, index) => new DiffEntry(DiffEntryKind.Added, index + 1, line)).ToList();
        }

        var before = SplitLines(oldText);
        var start = 0;
        while (start < before.Length && start < after.Length && string.Equals(before[start], after[start], StringComparison.Ordinal))
        {
            start++;
        }

        var endBefore = before.Length;
        var endAfter = after.Length;
        while (endBefore > start && endAfter > start && string.Equals(before[endBefore - 1], after[endAfter - 1], StringComparison.Ordinal))
        {
            endBefore--;
            endAfter--;
        }

        var entries = new List<DiffEntry>(before.Length + after.Length);
        for (var index = 0; index < start; index++)
        {
            entries.Add(new DiffEntry(DiffEntryKind.Context, index + 1, before[index]));
        }

        AppendChanged(entries, before, after, start, endBefore, endAfter);

        var suffixNumber = endAfter + 1;
        for (var index = endBefore; index < before.Length; index++)
        {
            entries.Add(new DiffEntry(DiffEntryKind.Context, suffixNumber++, before[index]));
        }

        return TrimContext(entries);
    }

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        // A final line break ends the last line; it does not start another one.
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    /// <summary>Appends the lines between the shared head and the shared tail, which are the only ones that can differ.</summary>
    private static void AppendChanged(List<DiffEntry> entries, string[] before, string[] after, int start, int endBefore, int endAfter)
    {
        var oldCount = endBefore - start;
        var newCount = endAfter - start;
        if (oldCount == 0 || newCount == 0 || (long)(oldCount + 1) * (newCount + 1) > MaxTableCells)
        {
            AppendReplacement(entries, before, after, start, endBefore, start, endAfter);
            return;
        }

        // common[i, j] is the length of the longest common subsequence of before[start + i ..] and after[start + j ..].
        var width = newCount + 1;
        var common = new int[(oldCount + 1) * width];
        for (var i = oldCount - 1; i >= 0; i--)
        {
            for (var j = newCount - 1; j >= 0; j--)
            {
                common[(i * width) + j] = string.Equals(before[start + i], after[start + j], StringComparison.Ordinal)
                    ? common[((i + 1) * width) + j + 1] + 1
                    : Math.Max(common[((i + 1) * width) + j], common[(i * width) + j + 1]);
            }
        }

        var row = 0;
        var column = 0;
        while (row < oldCount && column < newCount)
        {
            if (string.Equals(before[start + row], after[start + column], StringComparison.Ordinal))
            {
                entries.Add(new DiffEntry(DiffEntryKind.Context, start + column + 1, after[start + column]));
                row++;
                column++;
            }
            else if (common[((row + 1) * width) + column] >= common[(row * width) + column + 1])
            {
                entries.Add(new DiffEntry(DiffEntryKind.Removed, start + row + 1, before[start + row]));
                row++;
            }
            else
            {
                entries.Add(new DiffEntry(DiffEntryKind.Added, start + column + 1, after[start + column]));
                column++;
            }
        }

        AppendReplacement(entries, before, after, start + row, endBefore, start + column, endAfter);
    }

    /// <summary>Appends every old line from <paramref name="oldFrom"/> as removed and every new line from <paramref name="newFrom"/> as added.</summary>
    private static void AppendReplacement(List<DiffEntry> entries, string[] before, string[] after, int oldFrom, int oldTo, int newFrom, int newTo)
    {
        for (var index = oldFrom; index < oldTo; index++)
        {
            entries.Add(new DiffEntry(DiffEntryKind.Removed, index + 1, before[index]));
        }

        for (var index = newFrom; index < newTo; index++)
        {
            entries.Add(new DiffEntry(DiffEntryKind.Added, index + 1, after[index]));
        }
    }

    /// <summary>Keeps <see cref="ContextLines"/> context lines before the first change and after the last. A diff with no change keeps its first lines.</summary>
    private static List<DiffEntry> TrimContext(List<DiffEntry> entries)
    {
        var first = entries.FindIndex(static entry => entry.Kind != DiffEntryKind.Context);
        if (first < 0)
        {
            return entries.Take(ContextLines * 2).ToList();
        }

        var last = entries.FindLastIndex(static entry => entry.Kind != DiffEntryKind.Context);
        var lower = Math.Max(0, first - ContextLines);
        var upper = Math.Min(entries.Count, last + 1 + ContextLines);
        return entries.GetRange(lower, upper - lower);
    }
}
