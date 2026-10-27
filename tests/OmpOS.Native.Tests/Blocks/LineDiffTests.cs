using OmpOS.Native.Views.Blocks;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Blocks;

/// <summary>The line diff behind the file-edit card (cursor-agents-window-parity.md 4.6, diff block anatomy).</summary>
public sealed class LineDiffTests
{
    /// <summary>A file the edit created has every line added, numbered from one.</summary>
    [Fact]
    public void ACreatedFileIsAllAddedLines()
    {
        var entries = LineDiff.Compute(null, "a\nb\n");

        entries.ShouldBe([new DiffEntry(DiffEntryKind.Added, 1, "a"), new DiffEntry(DiffEntryKind.Added, 2, "b")]);
    }

    /// <summary>A change in the middle of a long file keeps three lines of context on each side, and the gutter numbers follow the new text.</summary>
    [Fact]
    public void AMiddleChangeKeepsThreeLinesOfContextEachSide()
    {
        var before = string.Join('\n', Enumerable.Range(1, 12).Select(static i => $"line {i}"));
        var after = before.Replace("line 6", "changed 6", StringComparison.Ordinal);

        var entries = LineDiff.Compute(before, after);

        entries.Select(static entry => (entry.Kind, entry.Number, entry.Text)).ShouldBe(
        [
            (DiffEntryKind.Context, 3, "line 3"),
            (DiffEntryKind.Context, 4, "line 4"),
            (DiffEntryKind.Context, 5, "line 5"),
            (DiffEntryKind.Removed, 6, "line 6"),
            (DiffEntryKind.Added, 6, "changed 6"),
            (DiffEntryKind.Context, 7, "line 7"),
            (DiffEntryKind.Context, 8, "line 8"),
            (DiffEntryKind.Context, 9, "line 9"),
        ]);
    }

    /// <summary>Lines the two texts share between two changes stay context instead of being redrawn as removed and added.</summary>
    [Fact]
    public void SharedLinesBetweenTwoChangesStayContext()
    {
        var entries = LineDiff.Compute("a\nb\nc\nd\ne", "a\nB\nc\nD\ne");

        entries.Count(static entry => entry.Kind == DiffEntryKind.Removed).ShouldBe(2);
        entries.Count(static entry => entry.Kind == DiffEntryKind.Added).ShouldBe(2);
        entries.Count(static entry => entry.Kind == DiffEntryKind.Context).ShouldBe(3);
    }

    /// <summary>A text with line breaks of both kinds and a final break splits into the same lines as one without.</summary>
    [Fact]
    public void LineBreaksOfBothKindsAndAFinalBreakDoNotAddLines()
    {
        LineDiff.Compute("a\r\nb\r\n", "a\nb").ShouldAllBe(static entry => entry.Kind == DiffEntryKind.Context);
        LineDiff.Compute("a\r\nb\r\n", "a\nb").Count.ShouldBe(2);
    }
}
