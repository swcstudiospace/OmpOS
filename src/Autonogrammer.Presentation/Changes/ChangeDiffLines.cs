using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Changes;

/// <summary>
/// The lines a file-edit card draws: the lines of the hunks <see cref="ChangeDiff"/> makes for an edit, in order, each with the number a
/// gutter shows for it. The card and the diff review therefore show the same lines for the same edit.
/// </summary>
public static class ChangeDiffLines
{
    /// <summary>Lists the lines of the hunks that turn one text into another, with their gutter numbers.</summary>
    /// <param name="oldText">The text before the edits, or null for a file the edits created: then every line is added.</param>
    /// <param name="newText">The text after the edits.</param>
    /// <returns>The lines of every hunk, in order. It is empty when the texts are equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newText"/> is null.</exception>
    public static IReadOnlyList<NumberedDiffLine> Numbered(string? oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(newText);

        var lines = new List<NumberedDiffLine>();
        foreach (var hunk in ChangeDiff.Create(oldText ?? string.Empty, newText).Hunks)
        {
            AppendHunk(lines, hunk);
        }

        return lines.AsReadOnly();
    }

    // A zero count names the line before the change, so the first line of an empty range is the one after it.
    private static void AppendHunk(List<NumberedDiffLine> lines, DiffHunk hunk)
    {
        var oldNumber = hunk.OldCount > 0 ? hunk.OldStart : hunk.OldStart + 1;
        var newNumber = hunk.NewCount > 0 ? hunk.NewStart : hunk.NewStart + 1;
        foreach (var line in hunk.Lines)
        {
            switch (line.Kind)
            {
                case DiffLineKind.Removed:
                    lines.Add(new NumberedDiffLine(line.Kind, oldNumber++, line.Text));
                    break;
                case DiffLineKind.Added:
                    lines.Add(new NumberedDiffLine(line.Kind, newNumber++, line.Text));
                    break;
                default:
                    lines.Add(new NumberedDiffLine(line.Kind, newNumber++, line.Text));
                    oldNumber++;
                    break;
            }
        }
    }
}
