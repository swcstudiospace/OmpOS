namespace OmpOS.Presentation.Editor;

/// <summary>
/// One hunk of a unified diff: its line ranges from the <c>@@</c> header and its lines. The ranges are one-based as the header
/// writes them; a zero count names the line before the change, so the hunk inserts after that line.
/// </summary>
/// <param name="OldStart">The first line of the original range, one-based.</param>
/// <param name="OldCount">The number of original lines in the hunk.</param>
/// <param name="NewStart">The first line of the new range, one-based.</param>
/// <param name="NewCount">The number of new lines in the hunk.</param>
/// <param name="Lines">The hunk's lines in diff order.</param>
/// <param name="OldEndsWithNewline">False when the original's last line lacks a line break (a <c>\ No newline</c> marker follows an original line).</param>
/// <param name="NewEndsWithNewline">False when the new text's last line lacks a line break.</param>
public sealed record DiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    IReadOnlyList<DiffLine> Lines,
    bool OldEndsWithNewline,
    bool NewEndsWithNewline)
{
    /// <summary>Gets the zero-based index of the first original line the hunk covers. For a zero count, the index of the line it inserts before.</summary>
    public int OldIndex => OldCount == 0 ? OldStart : OldStart - 1;

    /// <summary>Gets the lines the hunk expects in the original: its context and removed lines, in order.</summary>
    public IReadOnlyList<string> OldLines => Lines.Where(line => line.Kind != DiffLineKind.Added).Select(line => line.Text).ToArray();

    /// <summary>Gets the lines the hunk produces: its context and added lines, in order.</summary>
    public IReadOnlyList<string> NewLines => Lines.Where(line => line.Kind != DiffLineKind.Removed).Select(line => line.Text).ToArray();
}
