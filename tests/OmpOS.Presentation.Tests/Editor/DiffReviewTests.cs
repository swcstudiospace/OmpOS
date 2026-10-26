using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>Per-hunk decisions and the resulting file text (ADE-11): a rejected hunk reverts only itself.</summary>
public sealed class DiffReviewTests
{
    private const string Original = "alpha\nbeta\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n";
    private const string NewText = "alpha\nBETA\ngamma\ndelta\nepsilon\nzeta\neta\niota\ntheta\n";

    /// <summary>Accepting the first hunk and rejecting the second gives the exact mixed file.</summary>
    [Fact]
    public void AcceptOneHunk_RejectTheOther_GivesTheExactMixedText()
    {
        var review = Review(Original, UnifiedDiffTests.TwoHunks);

        review.Decide(0, HunkDecision.Accept);
        review.Decide(1, HunkDecision.Reject);

        review.Apply().ShouldBe("alpha\nBETA\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n");
    }

    /// <summary>Rejecting the first hunk and accepting the second gives the other exact mixed file.</summary>
    [Fact]
    public void RejectFirstHunk_AcceptSecond_GivesTheExactMixedText()
    {
        var review = Review(Original, UnifiedDiffTests.TwoHunks);

        review.Decide(0, HunkDecision.Reject);
        review.Decide(1, HunkDecision.Accept);

        review.Apply().ShouldBe("alpha\nbeta\ngamma\ndelta\nepsilon\nzeta\neta\niota\ntheta\n");
    }

    /// <summary>With every hunk pending, the result is the original.</summary>
    [Fact]
    public void AllPending_LeavesTheOriginal()
    {
        var review = Review(Original, UnifiedDiffTests.TwoHunks);

        review.Apply().ShouldBe(Original);
    }

    /// <summary>With every hunk accepted, the result is the diff's new text.</summary>
    [Fact]
    public void AllAccepted_EqualsTheNewText()
    {
        var review = Review(Original, UnifiedDiffTests.TwoHunks);

        review.Decide(0, HunkDecision.Accept);
        review.Decide(1, HunkDecision.Accept);

        review.Apply().ShouldBe(NewText);
    }

    /// <summary>Every hunk starts pending, and the pending count follows the decisions.</summary>
    [Fact]
    public void Decisions_DefaultToPending_AndTheCountFollowsThem()
    {
        var review = Review(Original, UnifiedDiffTests.TwoHunks);

        review.Hunks.Select(hunk => hunk.Decision).ShouldBe([HunkDecision.Pending, HunkDecision.Pending]);
        review.PendingCount.ShouldBe(2);

        review.Decide(1, HunkDecision.Reject);

        review.PendingCount.ShouldBe(1);
        review.Hunks[1].Decision.ShouldBe(HunkDecision.Reject);
    }

    /// <summary>A later decision replaces an earlier one on the same hunk.</summary>
    [Fact]
    public void Decide_ALaterDecisionReplacesAnEarlierOne()
    {
        var review = Review(Original, UnifiedDiffTests.TwoHunks);

        review.Decide(0, HunkDecision.Accept);
        review.Decide(0, HunkDecision.Reject);

        review.Apply().ShouldBe(Original);
    }

    /// <summary>A decision on a hunk that does not exist is refused.</summary>
    [Fact]
    public void Decide_OutOfRange_IsRefused()
    {
        var review = Review(Original, UnifiedDiffTests.TwoHunks);

        Should.Throw<ArgumentOutOfRangeException>(() => review.Decide(2, HunkDecision.Accept));
    }

    /// <summary>A diff that does not match the text it is reviewed against is refused.</summary>
    [Fact]
    public void Review_OfADifferentOriginal_IsRefused()
    {
        Should.Throw<ArgumentException>(() => new DiffReview("different\n", UnifiedDiff.Parse(UnifiedDiffTests.TwoHunks)));
    }

    /// <summary>A diff that disagrees with the original about the final line break is refused.</summary>
    [Fact]
    public void Review_DisagreeingAboutTheFinalLineBreak_IsRefused()
    {
        var noNewline = UnifiedDiff.Parse("@@ -1,2 +1,2 @@\n one\n-two\n\\ No newline at end of file\n+three\n");

        Should.Throw<ArgumentException>(() => new DiffReview("one\ntwo\n", noNewline));
    }

    /// <summary>Accepting or rejecting a change to the last line keeps the missing final line break the diff describes.</summary>
    [Fact]
    public void NoNewlineAtEnd_AcceptAndRejectKeepTheDescribedBreak()
    {
        var diff = UnifiedDiff.Parse("@@ -1,2 +1,2 @@\n one\n-two\n\\ No newline at end of file\n+three\n\\ No newline at end of file\n");

        var accepted = new DiffReview("one\ntwo", diff);
        accepted.Decide(0, HunkDecision.Accept);
        accepted.Apply().ShouldBe("one\nthree");

        var rejected = new DiffReview("one\ntwo", diff);
        rejected.Decide(0, HunkDecision.Reject);
        rejected.Apply().ShouldBe("one\ntwo");
    }

    /// <summary>An insertion into an empty file, accepted, gives the inserted lines with their final break.</summary>
    [Fact]
    public void InsertionIntoAnEmptyOriginal_AcceptGivesTheLines()
    {
        var diff = UnifiedDiff.Parse("@@ -0,0 +1,2 @@\n+a\n+b\n");

        var accepted = new DiffReview(string.Empty, diff);
        accepted.Decide(0, HunkDecision.Accept);
        accepted.Apply().ShouldBe("a\nb\n");

        new DiffReview(string.Empty, diff).Apply().ShouldBe(string.Empty);
    }

    private static DiffReview Review(string original, string diffText) => new(original, UnifiedDiff.Parse(diffText));
}
