namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// The review of an inline edit's proposal (ADE-12 into ADE-11). A proposal does not replace the selection. It opens as a
/// <see cref="DiffReview"/> of the whole buffer: the diff between the text as it is and the text with the proposal in place of the
/// selection, one hunk per separate change. Nothing reaches the buffer until <see cref="ApplyTo"/>, and then only the accepted hunks do.
/// </summary>
/// <remarks>
/// The review runs next to <see cref="InlineEdit"/>, not through it: the edit keeps its own accept, reject and undo for a proposal
/// taken whole, and this review decides per hunk. The diff is made line by line, so a selection that starts or ends inside a line
/// shows the whole of each touched line as removed and added.
/// </remarks>
public sealed class InlineEditReview
{
    private const int ContextLines = 3;

    // The longest line-by-line comparison worth its table (lines on one side times lines on the other); a larger change is shown as one replacement.
    private const long MaxComparedCells = 4_000_000;

    private InlineEditReview(InlineEdit edit, DiffReview review, UnifiedDiff diff)
    {
        Edit = edit;
        Review = review;
        Diff = diff;
    }

    /// <summary>Gets the inline edit whose proposal is under review.</summary>
    public InlineEdit Edit { get; }

    /// <summary>Gets the review that holds a decision for each hunk. Every hunk starts pending.</summary>
    public DiffReview Review { get; }

    /// <summary>Gets the diff from the buffer as it was when the review opened to the buffer with the whole proposal applied.</summary>
    public UnifiedDiff Diff { get; }

    /// <summary>Gets a value indicating whether the proposal changes anything. A proposal equal to the selection has no hunk.</summary>
    public bool HasChanges => Diff.Hunks.Count > 0;

    /// <summary>Opens a proposal as a reviewable diff. The buffer is not changed.</summary>
    /// <param name="document">The buffer that holds the edit's selection.</param>
    /// <param name="edit">The edit, with a proposal.</param>
    /// <returns>The review.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="edit"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The edit has no proposal under review, or the buffer no longer holds the captured selection at its range.
    /// </exception>
    public static InlineEditReview Open(TextDocument document, InlineEdit edit)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edit);

        if (edit.Status != InlineEditStatus.Proposed || edit.Proposal is null)
        {
            throw new InvalidOperationException("Only a proposed inline edit can be opened for review.");
        }

        var original = document.Text;
        var end = edit.Start + edit.Length;
        if (end > original.Length || !original.AsSpan(edit.Start, edit.Length).SequenceEqual(edit.SelectedText))
        {
            throw new InvalidOperationException("The buffer changed after the selection was captured, so the proposal no longer applies.");
        }

        var proposed = string.Concat(original.AsSpan(0, edit.Start), edit.Proposal, original.AsSpan(end));
        var diff = BuildDiff(original, proposed);
        return new InlineEditReview(edit, new DiffReview(original, diff), diff);
    }

    /// <summary>Writes the accepted hunks into the buffer. Rejected and pending hunks leave their lines as they are.</summary>
    /// <param name="document">The buffer the review was opened on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The buffer changed since the review was opened. Nothing is changed.</exception>
    public void ApplyTo(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var original = Review.Original;
        if (!string.Equals(document.Text, original, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The buffer changed after the proposal was opened for review, so the accepted hunks no longer apply.");
        }

        var result = Review.Apply();
        var prefix = original.AsSpan().CommonPrefixLength(result.AsSpan());
        if (prefix == original.Length && prefix == result.Length)
        {
            return;
        }

        var suffix = 0;
        var limit = Math.Min(original.Length, result.Length) - prefix;
        while (suffix < limit && original[original.Length - 1 - suffix] == result[result.Length - 1 - suffix])
        {
            suffix++;
        }

        document.Replace(prefix, original.Length - prefix - suffix, result.Substring(prefix, result.Length - prefix - suffix));
    }

    private static UnifiedDiff BuildDiff(string original, string proposed)
    {
        var oldLines = SplitText(original, out var oldEndsWithNewline);
        var newLines = SplitText(proposed, out var newEndsWithNewline);

        // A change of the final line break alone has no line difference, so the last line is always compared as changed then.
        var breakChanged = oldEndsWithNewline != newEndsWithNewline;
        var common = Math.Min(oldLines.Length, newLines.Length);

        var prefix = 0;
        var prefixLimit = breakChanged ? Math.Max(common - 1, 0) : common;
        while (prefix < prefixLimit && string.Equals(oldLines[prefix], newLines[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        var suffix = 0;
        while (!breakChanged
            && suffix < common - prefix
            && string.Equals(oldLines[oldLines.Length - 1 - suffix], newLines[newLines.Length - 1 - suffix], StringComparison.Ordinal))
        {
            suffix++;
        }

        var oldMiddle = oldLines[prefix..(oldLines.Length - suffix)];
        var newMiddle = newLines[prefix..(newLines.Length - suffix)];

        var contextBefore = Math.Min(ContextLines, prefix);
        var contextAfter = Math.Min(ContextLines, suffix);
        var operations = new List<Operation>(oldMiddle.Length + newMiddle.Length + contextBefore + contextAfter);
        var oldPosition = prefix - contextBefore;
        var newPosition = oldPosition;

        for (var index = prefix - contextBefore; index < prefix; index++)
        {
            operations.Add(new Operation(DiffLineKind.Context, oldLines[index], oldPosition++, newPosition++));
        }

        AddMiddle(operations, oldMiddle, newMiddle, breakChanged, ref oldPosition, ref newPosition);

        for (var index = 0; index < contextAfter; index++)
        {
            operations.Add(new Operation(DiffLineKind.Context, oldLines[oldLines.Length - suffix + index], oldPosition++, newPosition++));
        }

        return new UnifiedDiff(null, null, GroupIntoHunks(operations, oldLines.Length, newLines.Length, oldEndsWithNewline, newEndsWithNewline));
    }

    private static void AddMiddle(List<Operation> operations, string[] oldMiddle, string[] newMiddle, bool breakChanged, ref int oldPosition, ref int newPosition)
    {
        if (!breakChanged)
        {
            AddComparison(operations, oldMiddle, newMiddle, ref oldPosition, ref newPosition);
            return;
        }

        // Compare everything but the last line of each side; the last lines are then removed and added in full.
        var oldBody = oldMiddle.Length > 0 ? oldMiddle[..^1] : oldMiddle;
        var newBody = newMiddle.Length > 0 ? newMiddle[..^1] : newMiddle;
        AddComparison(operations, oldBody, newBody, ref oldPosition, ref newPosition);
        if (oldMiddle.Length > 0)
        {
            operations.Add(new Operation(DiffLineKind.Removed, oldMiddle[^1], oldPosition++, newPosition));
        }

        if (newMiddle.Length > 0)
        {
            operations.Add(new Operation(DiffLineKind.Added, newMiddle[^1], oldPosition, newPosition++));
        }
    }

    // Walks the longest common subsequence of the two sides. A change reads as the removed lines first, then the added ones.
    private static void AddComparison(List<Operation> operations, string[] oldSide, string[] newSide, ref int oldPosition, ref int newPosition)
    {
        if (oldSide.Length == 0 || newSide.Length == 0 || (long)oldSide.Length * newSide.Length > MaxComparedCells)
        {
            foreach (var line in oldSide)
            {
                operations.Add(new Operation(DiffLineKind.Removed, line, oldPosition++, newPosition));
            }

            foreach (var line in newSide)
            {
                operations.Add(new Operation(DiffLineKind.Added, line, oldPosition, newPosition++));
            }

            return;
        }

        var width = newSide.Length + 1;
        var common = new int[(oldSide.Length + 1) * width];
        for (var row = oldSide.Length - 1; row >= 0; row--)
        {
            for (var column = newSide.Length - 1; column >= 0; column--)
            {
                common[(row * width) + column] = string.Equals(oldSide[row], newSide[column], StringComparison.Ordinal)
                    ? common[((row + 1) * width) + column + 1] + 1
                    : Math.Max(common[((row + 1) * width) + column], common[(row * width) + column + 1]);
            }
        }

        var oldIndex = 0;
        var newIndex = 0;
        while (oldIndex < oldSide.Length && newIndex < newSide.Length)
        {
            if (string.Equals(oldSide[oldIndex], newSide[newIndex], StringComparison.Ordinal))
            {
                operations.Add(new Operation(DiffLineKind.Context, oldSide[oldIndex], oldPosition++, newPosition++));
                oldIndex++;
                newIndex++;
            }
            else if (common[((oldIndex + 1) * width) + newIndex] >= common[(oldIndex * width) + newIndex + 1])
            {
                operations.Add(new Operation(DiffLineKind.Removed, oldSide[oldIndex++], oldPosition++, newPosition));
            }
            else
            {
                operations.Add(new Operation(DiffLineKind.Added, newSide[newIndex++], oldPosition, newPosition++));
            }
        }

        for (; oldIndex < oldSide.Length; oldIndex++)
        {
            operations.Add(new Operation(DiffLineKind.Removed, oldSide[oldIndex], oldPosition++, newPosition));
        }

        for (; newIndex < newSide.Length; newIndex++)
        {
            operations.Add(new Operation(DiffLineKind.Added, newSide[newIndex], oldPosition, newPosition++));
        }
    }

    // Changes less than two context windows apart share a hunk; each hunk carries up to ContextLines of context on both sides.
    private static List<DiffHunk> GroupIntoHunks(List<Operation> operations, int oldLineCount, int newLineCount, bool oldEndsWithNewline, bool newEndsWithNewline)
    {
        var changes = new List<int>();
        for (var index = 0; index < operations.Count; index++)
        {
            if (operations[index].Kind != DiffLineKind.Context)
            {
                changes.Add(index);
            }
        }

        var hunks = new List<DiffHunk>();
        var first = 0;
        while (first < changes.Count)
        {
            var last = first;
            while (last + 1 < changes.Count && changes[last + 1] - changes[last] - 1 <= 2 * ContextLines)
            {
                last++;
            }

            var from = Math.Max(changes[first] - ContextLines, 0);
            var to = Math.Min(changes[last] + ContextLines, operations.Count - 1);
            var lines = new List<DiffLine>(to - from + 1);
            var oldCount = 0;
            var newCount = 0;
            for (var index = from; index <= to; index++)
            {
                var operation = operations[index];
                lines.Add(new DiffLine(operation.Kind, operation.Text));
                oldCount += operation.Kind == DiffLineKind.Added ? 0 : 1;
                newCount += operation.Kind == DiffLineKind.Removed ? 0 : 1;
            }

            var oldIndex = operations[from].OldPosition;
            var newIndex = operations[from].NewPosition;

            // Only a hunk that reaches the end of the text says anything about its final line break.
            var reachesEnd = oldIndex + oldCount == oldLineCount && newIndex + newCount == newLineCount;
            hunks.Add(new DiffHunk(
                oldCount == 0 ? oldIndex : oldIndex + 1,
                oldCount,
                newCount == 0 ? newIndex : newIndex + 1,
                newCount,
                lines.AsReadOnly(),
                !reachesEnd || oldEndsWithNewline,
                !reachesEnd || newEndsWithNewline));
            first = last + 1;
        }

        return hunks;
    }

    // The same split as DiffReview: the empty text has no lines, and text ending in '\n' has no extra empty line after its final break.
    private static string[] SplitText(string text, out bool endsWithNewline)
    {
        if (text.Length == 0)
        {
            endsWithNewline = true;
            return [];
        }

        endsWithNewline = text[^1] == '\n';
        return (endsWithNewline ? text[..^1] : text).Split('\n');
    }

    // One line of the comparison with the zero-based index of the next original and new line it sits before.
    private readonly record struct Operation(DiffLineKind Kind, string Text, int OldPosition, int NewPosition);
}
