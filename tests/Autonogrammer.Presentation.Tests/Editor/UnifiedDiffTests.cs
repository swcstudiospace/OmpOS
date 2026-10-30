using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>Parsing unified diffs into hunks with their ranges and line kinds (ADE-11).</summary>
public sealed class UnifiedDiffTests
{
    /// <summary>The two-hunk fixture used across the diff tests.</summary>
    internal const string TwoHunks =
        "--- a/notes.txt\n"
        + "+++ b/notes.txt\n"
        + "@@ -1,4 +1,4 @@\n"
        + " alpha\n"
        + "-beta\n"
        + "+BETA\n"
        + " gamma\n"
        + " delta\n"
        + "@@ -6,3 +6,4 @@\n"
        + " zeta\n"
        + " eta\n"
        + "+iota\n"
        + " theta\n";

    /// <summary>The headers and both hunks come out with their ranges and their lines in order.</summary>
    [Fact]
    public void Parse_ReadsHeadersRangesAndLinesInOrder()
    {
        var diff = UnifiedDiff.Parse(TwoHunks);

        diff.OldPath.ShouldBe("a/notes.txt");
        diff.NewPath.ShouldBe("b/notes.txt");
        diff.Hunks.Count.ShouldBe(2);

        var first = diff.Hunks[0];
        (first.OldStart, first.OldCount, first.NewStart, first.NewCount).ShouldBe((1, 4, 1, 4));
        first.Lines.Select(line => line.Kind).ShouldBe(
            [DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added, DiffLineKind.Context, DiffLineKind.Context]);
        first.Lines[1].Text.ShouldBe("beta");
        first.Lines[2].Text.ShouldBe("BETA");

        var second = diff.Hunks[1];
        (second.OldStart, second.OldCount, second.NewStart, second.NewCount).ShouldBe((6, 3, 6, 4));
        second.OldLines.ShouldBe(["zeta", "eta", "theta"]);
        second.NewLines.ShouldBe(["zeta", "eta", "iota", "theta"]);
    }

    /// <summary>A range without a count means one line, as the unified format defines it.</summary>
    [Fact]
    public void Parse_OmittedCountMeansOneLine()
    {
        var diff = UnifiedDiff.Parse("@@ -3 +3 @@\n-a\n+b\n");

        (diff.Hunks[0].OldCount, diff.Hunks[0].NewCount).ShouldBe((1, 1));
    }

    /// <summary>A no-newline marker after the last line says that side's text has no final line break.</summary>
    [Fact]
    public void Parse_NoNewlineMarker_MarksTheSideWithoutALineBreak()
    {
        var diff = UnifiedDiff.Parse("@@ -1,2 +1,2 @@\n one\n-two\n\\ No newline at end of file\n+three\n\\ No newline at end of file\n");

        diff.Hunks[0].OldEndsWithNewline.ShouldBeFalse();
        diff.Hunks[0].NewEndsWithNewline.ShouldBeFalse();
    }

    /// <summary>A diff with no hunk is refused.</summary>
    [Fact]
    public void Parse_RefusesADiffWithNoHunk()
    {
        Should.Throw<FormatException>(() => UnifiedDiff.Parse("--- a\n+++ b\n"));
    }

    /// <summary>A second file after the hunks is refused: one diff is one file.</summary>
    [Fact]
    public void Parse_RefusesASecondFile()
    {
        Should.Throw<FormatException>(() => UnifiedDiff.Parse(TwoHunks + "--- c/other.txt\n+++ d/other.txt\n@@ -1 +1 @@\n-a\n+b\n"));
    }

    /// <summary>Lines that do not fit the header's counts are refused rather than guessed at.</summary>
    [Fact]
    public void Parse_RefusesLinesThatDoNotFitTheCounts()
    {
        Should.Throw<FormatException>(() => UnifiedDiff.Parse("@@ -1,2 +1,1 @@\n a\n-b\n+c\n+d\n"));
    }

    /// <summary>A hunk header that is not a valid range is refused.</summary>
    [Fact]
    public void Parse_RefusesAMalformedHeader()
    {
        Should.Throw<FormatException>(() => UnifiedDiff.Parse("@@ nonsense @@\n-a\n+b\n"));
    }
}
