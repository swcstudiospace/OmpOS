using System.Collections.ObjectModel;
using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Changes;

/// <summary>
/// Computes the line diff between the text of a file before and after an agent's edits, as the hunks a <see cref="DiffReview"/> decides.
/// The longest common subsequence of the lines decides which lines are shared; texts too large for that table are shown as one block
/// replacement between their shared first and last lines. A final line that lacks its line break differs from the same line with one,
/// so a change that only adds or removes the final break is a hunk too.
/// </summary>
public static class ChangeDiff
{
    /// <summary>The number of unchanged lines kept before and after each change in a hunk.</summary>
    public const int ContextLines = 3;

    /// <summary>The largest table of the common-subsequence search, in cells. Beyond it the changed region is one block replacement.</summary>
    private const long MaxTableCells = 1_000_000;

    /// <summary>Computes the diff that turns one text into another.</summary>
    /// <param name="oldText">The text before the edits.</param>
    /// <param name="newText">The text after the edits.</param>
    /// <returns>The diff. It has no hunk when the texts are equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="oldText"/> or <paramref name="newText"/> is null.</exception>
    public static UnifiedDiff Create(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var before = Split(oldText);
        var after = Split(newText);
        var ops = Operations(before, after);
        return new UnifiedDiff(null, null, Hunks(ops, before, after));
    }

    /// <summary>Counts the lines a diff adds and removes.</summary>
    /// <param name="oldText">The text before the edits, or null for a file the edits created.</param>
    /// <param name="newText">The text after the edits.</param>
    /// <returns>The number of added and of removed lines.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newText"/> is null.</exception>
    public static (int Added, int Removed) Count(string? oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(newText);

        var ops = Operations(Split(oldText ?? string.Empty), Split(newText));
        var added = 0;
        var removed = 0;
        foreach (var operation in ops)
        {
            if (operation.Kind == DiffLineKind.Added)
            {
                added++;
            }
            else if (operation.Kind == DiffLineKind.Removed)
            {
                removed++;
            }
        }

        return (added, removed);
    }

    // The empty text has no lines; text ending in '\n' has no extra empty line after its final break.
    private static SplitText Split(string text)
    {
        if (text.Length == 0)
        {
            return new SplitText([], EndsWithNewline: true);
        }

        var endsWithNewline = text[^1] == '\n';
        return new SplitText((endsWithNewline ? text[..^1] : text).Split('\n'), endsWithNewline);
    }

    // Two lines are the same when their text is and they agree on whether a line break follows. Only a last line can lack one.
    private static bool Same(SplitText left, int leftIndex, SplitText right, int rightIndex) =>
        string.Equals(left.Lines[leftIndex], right.Lines[rightIndex], StringComparison.Ordinal)
        && left.IsUnterminated(leftIndex) == right.IsUnterminated(rightIndex);

    private static List<Operation> Operations(SplitText before, SplitText after)
    {
        var start = 0;
        while (start < before.Lines.Length && start < after.Lines.Length && Same(before, start, after, start))
        {
            start++;
        }

        var endBefore = before.Lines.Length;
        var endAfter = after.Lines.Length;
        while (endBefore > start && endAfter > start && Same(before, endBefore - 1, after, endAfter - 1))
        {
            endBefore--;
            endAfter--;
        }

        var operations = new List<Operation>(before.Lines.Length + after.Lines.Length);
        var oldPosition = 0;
        var newPosition = 0;
        for (var index = 0; index < start; index++)
        {
            Add(operations, DiffLineKind.Context, before.Lines[index], ref oldPosition, ref newPosition);
        }

        var changed = new List<Operation>();
        AppendChanged(changed, before, after, start, endBefore, endAfter);

        // Each run of changes already lists its removed lines before its added ones, as a unified diff writes them.
        foreach (var operation in changed)
        {
            Add(operations, operation.Kind, operation.Text, ref oldPosition, ref newPosition);
        }

        for (var index = endBefore; index < before.Lines.Length; index++)
        {
            Add(operations, DiffLineKind.Context, before.Lines[index], ref oldPosition, ref newPosition);
        }

        return operations;
    }

    // The changed region between the shared head and the shared tail: shared lines inside it stay in order with the runs of changes.
    private static void AppendChanged(List<Operation> changed, SplitText before, SplitText after, int start, int endBefore, int endAfter)
    {
        var oldCount = endBefore - start;
        var newCount = endAfter - start;
        if (oldCount == 0 || newCount == 0 || (long)(oldCount + 1) * (newCount + 1) > MaxTableCells)
        {
            for (var index = start; index < endBefore; index++)
            {
                changed.Add(new Operation(DiffLineKind.Removed, before.Lines[index], 0, 0));
            }

            for (var index = start; index < endAfter; index++)
            {
                changed.Add(new Operation(DiffLineKind.Added, after.Lines[index], 0, 0));
            }

            return;
        }

        // lengths[i * width + j] is the length of the longest common subsequence of the old lines from i and the new lines from j.
        var width = newCount + 1;
        var lengths = new int[(oldCount + 1) * width];
        for (var oldIndex = oldCount - 1; oldIndex >= 0; oldIndex--)
        {
            for (var newIndex = newCount - 1; newIndex >= 0; newIndex--)
            {
                lengths[(oldIndex * width) + newIndex] = Same(before, start + oldIndex, after, start + newIndex)
                    ? lengths[((oldIndex + 1) * width) + newIndex + 1] + 1
                    : Math.Max(lengths[((oldIndex + 1) * width) + newIndex], lengths[(oldIndex * width) + newIndex + 1]);
            }
        }

        var oldCursor = 0;
        var newCursor = 0;
        var pendingRemoved = new List<Operation>();
        var pendingAdded = new List<Operation>();
        while (oldCursor < oldCount || newCursor < newCount)
        {
            if (oldCursor < oldCount && newCursor < newCount && Same(before, start + oldCursor, after, start + newCursor))
            {
                FlushRun(changed, pendingRemoved, pendingAdded);
                changed.Add(new Operation(DiffLineKind.Context, before.Lines[start + oldCursor], 0, 0));
                oldCursor++;
                newCursor++;
            }
            else if (newCursor >= newCount || (oldCursor < oldCount && lengths[((oldCursor + 1) * width) + newCursor] >= lengths[(oldCursor * width) + newCursor + 1]))
            {
                pendingRemoved.Add(new Operation(DiffLineKind.Removed, before.Lines[start + oldCursor], 0, 0));
                oldCursor++;
            }
            else
            {
                pendingAdded.Add(new Operation(DiffLineKind.Added, after.Lines[start + newCursor], 0, 0));
                newCursor++;
            }
        }

        FlushRun(changed, pendingRemoved, pendingAdded);
    }

    private static void FlushRun(List<Operation> changed, List<Operation> removed, List<Operation> added)
    {
        changed.AddRange(removed);
        changed.AddRange(added);
        removed.Clear();
        added.Clear();
    }

    private static void Add(List<Operation> operations, DiffLineKind kind, string text, ref int oldPosition, ref int newPosition)
    {
        operations.Add(new Operation(kind, text, oldPosition, newPosition));
        if (kind != DiffLineKind.Added)
        {
            oldPosition++;
        }

        if (kind != DiffLineKind.Removed)
        {
            newPosition++;
        }
    }

    private static ReadOnlyCollection<DiffHunk> Hunks(List<Operation> operations, SplitText before, SplitText after)
    {
        var hunks = new List<DiffHunk>();
        var floor = 0;
        while (true)
        {
            var first = NextChange(operations, floor);
            if (first < 0)
            {
                break;
            }

            var last = first;
            while (true)
            {
                var next = NextChange(operations, last + 1);
                if (next >= 0 && next - last - 1 <= 2 * ContextLines)
                {
                    last = next;
                    continue;
                }

                break;
            }

            var start = Math.Max(floor, first - ContextLines);
            var end = Math.Min(operations.Count, last + 1 + ContextLines);
            hunks.Add(Hunk(operations, start, end, before, after));
            floor = end;
        }

        return hunks.AsReadOnly();
    }

    private static int NextChange(List<Operation> operations, int startIndex)
    {
        for (var index = startIndex; index < operations.Count; index++)
        {
            if (operations[index].Kind != DiffLineKind.Context)
            {
                return index;
            }
        }

        return -1;
    }

    private static DiffHunk Hunk(List<Operation> operations, int start, int end, SplitText before, SplitText after)
    {
        var lines = new List<DiffLine>(end - start);
        var oldCount = 0;
        var newCount = 0;
        for (var index = start; index < end; index++)
        {
            var operation = operations[index];
            lines.Add(new DiffLine(operation.Kind, operation.Text));
            if (operation.Kind != DiffLineKind.Added)
            {
                oldCount++;
            }

            if (operation.Kind != DiffLineKind.Removed)
            {
                newCount++;
            }
        }

        var oldPosition = operations[start].OldPosition;
        var newPosition = operations[start].NewPosition;
        var reachesOldEnd = oldPosition + oldCount == before.Lines.Length;
        var reachesNewEnd = newPosition + newCount == after.Lines.Length;
        return new DiffHunk(
            oldCount > 0 ? oldPosition + 1 : oldPosition,
            oldCount,
            newCount > 0 ? newPosition + 1 : newPosition,
            newCount,
            lines.AsReadOnly(),
            OldEndsWithNewline: !reachesOldEnd || before.EndsWithNewline,
            NewEndsWithNewline: !reachesNewEnd || after.EndsWithNewline);
    }

    /// <summary>A text split into lines, and whether its last line is followed by a line break.</summary>
    private readonly record struct SplitText(string[] Lines, bool EndsWithNewline)
    {
        /// <summary>Tells whether the line at an index is the last line of a text that does not end with a line break.</summary>
        public bool IsUnterminated(int index) => !EndsWithNewline && index == Lines.Length - 1;
    }

    /// <summary>One line of the full line-by-line edit script, with the number of old and new lines before it.</summary>
    private readonly record struct Operation(DiffLineKind Kind, string Text, int OldPosition, int NewPosition);
}
