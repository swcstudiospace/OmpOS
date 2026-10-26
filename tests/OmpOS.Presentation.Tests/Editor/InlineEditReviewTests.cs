using System.Globalization;
using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>An inline edit's proposal opens as a per-hunk diff review and reaches the buffer only through it (ADE-12 into ADE-11).</summary>
public sealed class InlineEditReviewTests
{
    private const string Source = "let total = a + b;\nreturn total;\n";

    /// <summary>The proposal shows as a removed and an added line with the unchanged line as context; the buffer and the edit stay untouched.</summary>
    [Fact]
    public void AProposalOpensAsAReviewableDiffNotADirectWrite()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, 12, 5, "use a product");
        edit.Propose("a * b");

        var opened = InlineEditReview.Open(document, edit);

        document.Text.ShouldBe(Source);
        edit.Status.ShouldBe(InlineEditStatus.Proposed);
        opened.HasChanges.ShouldBeTrue();
        opened.Review.Hunks.Count.ShouldBe(1);
        opened.Review.PendingCount.ShouldBe(1);
        opened.Diff.Hunks[0].Lines.ShouldBe(new DiffLine[]
        {
            new(DiffLineKind.Removed, "let total = a + b;"),
            new(DiffLineKind.Added, "let total = a * b;"),
            new(DiffLineKind.Context, "return total;"),
        });

        // Pending changes nothing; accepting the hunk is what produces the new text, and only applying it touches the buffer.
        opened.Review.Apply().ShouldBe(Source);
        opened.Review.Decide(0, HunkDecision.Accept);
        opened.Review.Apply().ShouldBe("let total = a * b;\nreturn total;\n");
        document.Text.ShouldBe(Source);

        opened.ApplyTo(document);

        document.Text.ShouldBe("let total = a * b;\nreturn total;\n");
    }

    /// <summary>A rejected hunk leaves the buffer as it was when the review is applied.</summary>
    [Fact]
    public void ARejectedProposalLeavesTheBufferUntouched()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, 12, 5, "use a product");
        edit.Propose("a * b");
        var opened = InlineEditReview.Open(document, edit);
        opened.Review.Decide(0, HunkDecision.Reject);

        opened.ApplyTo(document);

        document.Text.ShouldBe(Source);
    }

    /// <summary>Two changes far apart become two hunks that are decided separately; the rejected one keeps its lines.</summary>
    [Fact]
    public void SeparateChangesBecomeSeparateHunks_AndOnlyTheAcceptedOneIsApplied()
    {
        var lines = Enumerable.Range(1, 12).Select(number => "line" + number.ToString("00", CultureInfo.InvariantCulture)).ToArray();
        var source = string.Join('\n', lines) + "\n";
        var selectionStart = source.IndexOf("line02", StringComparison.Ordinal);
        var selectionEnd = source.IndexOf("line12", StringComparison.Ordinal);
        var selected = source[selectionStart..selectionEnd];
        var document = new TextDocument(source);
        var edit = new InlineEdit(document, selectionStart, selected.Length, "shout the ends");
        edit.Propose(selected.Replace("line02", "LINE02", StringComparison.Ordinal).Replace("line11", "LINE11", StringComparison.Ordinal));

        var opened = InlineEditReview.Open(document, edit);

        opened.Review.Hunks.Count.ShouldBe(2);
        opened.Review.Decide(0, HunkDecision.Reject);
        opened.Review.Decide(1, HunkDecision.Accept);
        opened.ApplyTo(document);

        document.Text.ShouldBe(source.Replace("line11", "LINE11", StringComparison.Ordinal));
    }

    /// <summary>A change that only drops the final line break is a change, and accepting it removes the break.</summary>
    [Fact]
    public void DroppingTheFinalLineBreak_IsAReviewableChange()
    {
        var document = new TextDocument("a\nb\n");
        var edit = new InlineEdit(document, 3, 1, "no trailing newline");
        edit.Propose(string.Empty);

        var opened = InlineEditReview.Open(document, edit);
        opened.HasChanges.ShouldBeTrue();
        opened.Review.Decide(0, HunkDecision.Accept);
        opened.ApplyTo(document);

        document.Text.ShouldBe("a\nb");
    }

    /// <summary>A proposal equal to the selection has no hunk and applying it changes nothing.</summary>
    [Fact]
    public void AProposalEqualToTheSelection_HasNoChanges()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, 12, 5, "keep");
        edit.Propose("a + b");

        var opened = InlineEditReview.Open(document, edit);
        opened.ApplyTo(document);

        opened.HasChanges.ShouldBeFalse();
        opened.Review.Hunks.ShouldBeEmpty();
        document.Text.ShouldBe(Source);
    }

    /// <summary>Accepting every hunk gives exactly what replacing the selection with the proposal gives, whatever the line breaks.</summary>
    [Theory]
    [InlineData("a\nb\nc\n", 2, 1, "B")]
    [InlineData("a\nb\nc", 4, 1, "C\nD")]
    [InlineData("a\nb\nc\n", 0, 6, "")]
    [InlineData("only", 0, 4, "")]
    [InlineData("a\nb", 2, 1, "b\n")]
    [InlineData("a\r\nb\r\n", 3, 1, "B")]
    [InlineData("x = f(a, b);\n", 6, 4, "a,\n    b")]
    [InlineData("a\nb\nc\n", 2, 1, "b\nb2\n")]
    [InlineData("a\nb\nc\n", 0, 1, "z\ny\nx")]
    public void AcceptingEveryHunk_EqualsReplacingTheSelectionDirectly(string source, int start, int length, string proposal)
    {
        var document = new TextDocument(source);
        var edit = new InlineEdit(document, start, length, "rewrite");
        edit.Propose(proposal);
        var expected = source[..start] + proposal + source[(start + length)..];

        var opened = InlineEditReview.Open(document, edit);
        for (var index = 0; index < opened.Review.Hunks.Count; index++)
        {
            opened.Review.Decide(index, HunkDecision.Accept);
        }

        opened.Review.Apply().ShouldBe(expected);
        opened.ApplyTo(document);
        document.Text.ShouldBe(expected);
    }

    /// <summary>An edit with no proposal, or whose selection no longer matches the buffer, cannot be opened.</summary>
    [Fact]
    public void Open_RefusesAnEditWithoutAProposalOrAStaleSelection()
    {
        var document = new TextDocument(Source);
        var awaiting = new InlineEdit(document, 12, 5, "use a product");
        Should.Throw<InvalidOperationException>(() => InlineEditReview.Open(document, awaiting));

        var edit = new InlineEdit(document, 12, 5, "use a product");
        edit.Propose("a * b");
        document.Replace(12, 5, "x - y");

        Should.Throw<InvalidOperationException>(() => InlineEditReview.Open(document, edit));
    }

    /// <summary>A buffer edited after the review opened refuses the accepted hunks and keeps its newer text.</summary>
    [Fact]
    public void ApplyTo_ABufferEditedAfterTheReviewOpened_IsRefused()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, 12, 5, "use a product");
        edit.Propose("a * b");
        var opened = InlineEditReview.Open(document, edit);
        opened.Review.Decide(0, HunkDecision.Accept);
        document.Replace(0, 3, "var");

        Should.Throw<InvalidOperationException>(() => opened.ApplyTo(document));

        document.Text.ShouldBe("var total = a + b;\nreturn total;\n");
    }
}
