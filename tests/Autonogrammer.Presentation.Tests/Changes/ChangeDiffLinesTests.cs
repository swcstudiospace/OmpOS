using Autonogrammer.Presentation.Changes;
using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// The lines a file-edit card draws. They come from the same hunks as the diff review, so the card and the review number and classify
/// every line of an edit alike.
/// </summary>
public sealed class ChangeDiffLinesTests
{
    /// <summary>A file the edit created has every line added, numbered from one.</summary>
    [Fact]
    public void ACreatedFileIsAllAddedLines()
    {
        var lines = ChangeDiffLines.Numbered(null, "a\nb\n");

        lines.ShouldBe([new NumberedDiffLine(DiffLineKind.Added, 1, "a"), new NumberedDiffLine(DiffLineKind.Added, 2, "b")]);
    }

    /// <summary>A change in the middle of a long file keeps three lines of context on each side, and the gutter numbers follow the new text.</summary>
    [Fact]
    public void AMiddleChangeKeepsThreeLinesOfContextEachSide()
    {
        var before = string.Join('\n', Enumerable.Range(1, 12).Select(static i => $"line {i}"));
        var after = before.Replace("line 6", "changed 6", StringComparison.Ordinal);

        var lines = ChangeDiffLines.Numbered(before, after);

        lines.Select(static line => (line.Kind, line.Number, line.Text)).ShouldBe(
        [
            (DiffLineKind.Context, 3, "line 3"),
            (DiffLineKind.Context, 4, "line 4"),
            (DiffLineKind.Context, 5, "line 5"),
            (DiffLineKind.Removed, 6, "line 6"),
            (DiffLineKind.Added, 6, "changed 6"),
            (DiffLineKind.Context, 7, "line 7"),
            (DiffLineKind.Context, 8, "line 8"),
            (DiffLineKind.Context, 9, "line 9"),
        ]);
    }

    /// <summary>Changes far apart are drawn as separate hunks, and each hunk's lines carry the numbers of their own place in the file.</summary>
    [Fact]
    public void FarApartChangesKeepTheirOwnLineNumbers()
    {
        var before = string.Join('\n', Enumerable.Range(1, 20).Select(static i => $"line {i}"));
        var after = before.Replace("line 2\n", "changed 2\n", StringComparison.Ordinal).Replace("line 15", "changed 15", StringComparison.Ordinal);

        var lines = ChangeDiffLines.Numbered(before, after);

        lines.Select(static line => (line.Kind, line.Number)).ShouldBe(
        [
            (DiffLineKind.Context, 1),
            (DiffLineKind.Removed, 2),
            (DiffLineKind.Added, 2),
            (DiffLineKind.Context, 3),
            (DiffLineKind.Context, 4),
            (DiffLineKind.Context, 5),
            (DiffLineKind.Context, 12),
            (DiffLineKind.Context, 13),
            (DiffLineKind.Context, 14),
            (DiffLineKind.Removed, 15),
            (DiffLineKind.Added, 15),
            (DiffLineKind.Context, 16),
            (DiffLineKind.Context, 17),
            (DiffLineKind.Context, 18),
        ]);
    }

    /// <summary>A final line break that is only added is a change of its line, so the line shows as removed and added.</summary>
    [Fact]
    public void AFinalLineBreakAloneIsAChange()
    {
        var lines = ChangeDiffLines.Numbered("a\nb", "a\nb\n");

        lines.Select(static line => (line.Kind, line.Number, line.Text)).ShouldBe(
        [
            (DiffLineKind.Context, 1, "a"),
            (DiffLineKind.Removed, 2, "b"),
            (DiffLineKind.Added, 2, "b"),
        ]);
    }

    /// <summary>Equal texts have no line to draw.</summary>
    [Fact]
    public void EqualTextsHaveNoLines()
    {
        ChangeDiffLines.Numbered("a\nb\n", "a\nb\n").ShouldBeEmpty();
    }

    /// <summary>The lines the card counts as added and removed are the ones the diff counts, for every pair of texts.</summary>
    [Theory]
    [InlineData("a\nb\nc\n", "a\nB\nc\n")]
    [InlineData("a\nb\n", "a\nb\nc\n")]
    [InlineData("a\nb\nc\n", "a\nc\n")]
    [InlineData("a\nb\n", "")]
    [InlineData("a\nb", "a\nb\n")]
    public void TheLinesCountTheSameAsTheDiff(string oldText, string newText)
    {
        var lines = ChangeDiffLines.Numbered(oldText, newText);

        var added = lines.Count(static line => line.Kind == DiffLineKind.Added);
        var removed = lines.Count(static line => line.Kind == DiffLineKind.Removed);
        (added, removed).ShouldBe(ChangeDiff.Count(oldText, newText));
    }
}
