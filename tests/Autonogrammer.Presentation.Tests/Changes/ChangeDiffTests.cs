using System.Globalization;
using Autonogrammer.Presentation.Changes;
using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>
/// The line diff behind the review of an agent's edits: accepting every hunk gives the new text, rejecting every hunk gives the old text,
/// and a change that only touches the final line break is a hunk too.
/// </summary>
public sealed class ChangeDiffTests
{
    /// <summary>For every pair of texts, a review of the diff gives the old text with no hunk accepted and the new text with all accepted.</summary>
    [Theory]
    [InlineData("a\nb\nc\n", "a\nB\nc\n")]
    [InlineData("a\nb\n", "a\nb\nc\n")]
    [InlineData("a\nb\nc\n", "a\nc\n")]
    [InlineData("", "a\nb\n")]
    [InlineData("a\nb\n", "")]
    [InlineData("a\nb", "a\nb\n")]
    [InlineData("a\nb\n", "a\nb")]
    [InlineData("a\nb", "a\nc")]
    [InlineData("one\n", "one\ntwo\nthree\n")]
    [InlineData("a\r\nb\r\n", "a\r\nc\r\n")]
    [InlineData("x\ny\nx\ny\n", "y\nx\ny\nx\n")]
    public void ReviewOfTheDiffRoundTripsBothTexts(string oldText, string newText)
    {
        var diff = ChangeDiff.Create(oldText, newText);

        var untouched = new DiffReview(oldText, diff);
        untouched.Apply().ShouldBe(oldText);

        var accepted = new DiffReview(oldText, diff);
        var rejected = new DiffReview(oldText, diff);
        for (var index = 0; index < diff.Hunks.Count; index++)
        {
            accepted.Decide(index, HunkDecision.Accept);
            rejected.Decide(index, HunkDecision.Reject);
        }

        accepted.Apply().ShouldBe(newText);
        rejected.Apply().ShouldBe(oldText);
    }

    /// <summary>Equal texts have no hunk.</summary>
    [Fact]
    public void EqualTextsHaveNoHunk()
    {
        ChangeDiff.Create("a\nb\n", "a\nb\n").Hunks.ShouldBeEmpty();
    }

    /// <summary>Changes far apart are separate hunks, and changes close together share one.</summary>
    [Fact]
    public void FarApartChangesAreSeparateHunksAndCloseOnesShareOne()
    {
        var lines = Enumerable.Range(1, 30).Select(number => "line " + number.ToString(CultureInfo.InvariantCulture)).ToArray();
        var oldText = string.Join('\n', lines) + "\n";

        var far = (string[])lines.Clone();
        far[1] = "changed 2";
        far[27] = "changed 28";
        var close = (string[])lines.Clone();
        close[1] = "changed 2";
        close[7] = "changed 8";

        ChangeDiff.Create(oldText, string.Join('\n', far) + "\n").Hunks.Count.ShouldBe(2);
        ChangeDiff.Create(oldText, string.Join('\n', close) + "\n").Hunks.Count.ShouldBe(1);
    }

    /// <summary>A hunk carries its context, so a one-line change in a long file shows where it is.</summary>
    [Fact]
    public void AHunkCarriesThreeLinesOfContextOnEachSide()
    {
        var lines = Enumerable.Range(1, 20).Select(number => "line " + number.ToString(CultureInfo.InvariantCulture)).ToArray();
        var changed = (string[])lines.Clone();
        changed[9] = "changed 10";

        var hunk = ChangeDiff.Create(string.Join('\n', lines) + "\n", string.Join('\n', changed) + "\n").Hunks.Single();

        hunk.Lines.Select(static line => line.Kind).ShouldBe(
        [
            DiffLineKind.Context, DiffLineKind.Context, DiffLineKind.Context,
            DiffLineKind.Removed, DiffLineKind.Added,
            DiffLineKind.Context, DiffLineKind.Context, DiffLineKind.Context,
        ]);
        hunk.OldStart.ShouldBe(7);
        hunk.OldCount.ShouldBe(7);
    }

    /// <summary>The counts are the lines the diff adds and removes; a created file adds every line.</summary>
    [Fact]
    public void CountsAreTheAddedAndRemovedLines()
    {
        ChangeDiff.Count("a\nb\nc\n", "a\nB\nc\n").ShouldBe((1, 1));
        ChangeDiff.Count(null, "a\nb\n").ShouldBe((2, 0));
        ChangeDiff.Count("a\nb\n", "a\n").ShouldBe((0, 1));
    }
}
