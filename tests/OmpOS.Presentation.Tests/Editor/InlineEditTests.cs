using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>Inline edit on a selection (ADE-12): the captured range, acceptance-gated replacement, rejection and undo.</summary>
public sealed class InlineEditTests
{
    private const string Source = "let total = a + b;";

    // The selection "a + b" starts at offset 12 and is five characters long.
    private const int SelectionStart = 12;
    private const int SelectionLength = 5;

    /// <summary>Creating the edit captures the selected range, its text and the instruction, and changes nothing.</summary>
    [Fact]
    public void Capture_RecordsTheRangeTextAndInstruction()
    {
        var document = new TextDocument(Source);

        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");

        edit.Start.ShouldBe(SelectionStart);
        edit.Length.ShouldBe(SelectionLength);
        edit.SelectedText.ShouldBe("a + b");
        edit.Instruction.ShouldBe("use a constant");
        edit.Status.ShouldBe(InlineEditStatus.AwaitingProposal);
        document.Text.ShouldBe(Source);
    }

    /// <summary>A proposal is held for review and does not touch the buffer.</summary>
    [Fact]
    public void Propose_HoldsTheReplacementWithoutChangingTheBuffer()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");

        edit.Propose("a * b");

        edit.Status.ShouldBe(InlineEditStatus.Proposed);
        edit.Proposal.ShouldBe("a * b");
        document.Text.ShouldBe(Source);
    }

    /// <summary>Accepting replaces exactly the captured range and leaves the rest of the text as it was.</summary>
    [Fact]
    public void Accept_ReplacesOnlyTheCapturedRange()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");
        edit.Propose("a * b");

        edit.Accept();

        document.Text.ShouldBe("let total = a * b;");
        edit.Status.ShouldBe(InlineEditStatus.Accepted);
    }

    /// <summary>Rejecting leaves the buffer unchanged, and the edit cannot then be accepted.</summary>
    [Fact]
    public void Reject_LeavesTheBufferUnchanged()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");
        edit.Propose("a * b");

        edit.Reject();

        document.Text.ShouldBe(Source);
        edit.Status.ShouldBe(InlineEditStatus.Rejected);
        Should.Throw<InvalidOperationException>(() => edit.Accept());
    }

    /// <summary>Undo after an accept restores the original text exactly.</summary>
    [Fact]
    public void Undo_RestoresTheOriginalText()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");
        edit.Propose("a * b");
        edit.Accept();

        edit.Undo();

        document.Text.ShouldBe(Source);
        edit.Status.ShouldBe(InlineEditStatus.Undone);
    }

    /// <summary>A proposal that replaces an earlier proposal is the one accepted.</summary>
    [Fact]
    public void Propose_ALaterProposalReplacesTheEarlierOne()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");
        edit.Propose("a");
        edit.Propose("b");

        edit.Accept();

        document.Text.ShouldBe("let total = b;");
    }

    /// <summary>An empty replacement deletes the selection.</summary>
    [Fact]
    public void Accept_OfAnEmptyProposal_DeletesTheSelection()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "remove it");
        edit.Propose(string.Empty);

        edit.Accept();

        document.Text.ShouldBe("let total = ;");
    }

    /// <summary>Accepting before any proposal is refused and the buffer is unchanged.</summary>
    [Fact]
    public void Accept_WithoutAProposal_IsRefused()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");

        Should.Throw<InvalidOperationException>(() => edit.Accept());
        document.Text.ShouldBe(Source);
    }

    /// <summary>When the buffer changed under the selection, accepting is refused rather than replacing the wrong text.</summary>
    [Fact]
    public void Accept_AfterTheBufferChangedUnderTheSelection_IsRefused()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");
        edit.Propose("a * b");
        document.Replace(0, 3, "const");

        Should.Throw<InvalidOperationException>(() => edit.Accept());
        document.Text.ShouldBe("const total = a + b;");
    }

    /// <summary>When the accepted text was edited, undo is refused and the newer text stays.</summary>
    [Fact]
    public void Undo_AfterTheAcceptedTextChanged_IsRefused()
    {
        var document = new TextDocument(Source);
        var edit = new InlineEdit(document, SelectionStart, SelectionLength, "use a constant");
        edit.Propose("a * b");
        edit.Accept();
        document.Replace(SelectionStart, 1, "z");

        Should.Throw<InvalidOperationException>(() => edit.Undo());
        document.Text.ShouldBe("let total = z * b;");
    }

    /// <summary>An empty selection, a blank instruction and a range outside the buffer are all refused.</summary>
    [Fact]
    public void Capture_RefusesAnEmptySelectionBlankInstructionAndOutOfRangeSelection()
    {
        var document = new TextDocument(Source);

        Should.Throw<ArgumentOutOfRangeException>(() => new InlineEdit(document, SelectionStart, 0, "x"));
        Should.Throw<ArgumentException>(() => new InlineEdit(document, SelectionStart, SelectionLength, "   "));
        Should.Throw<ArgumentOutOfRangeException>(() => new InlineEdit(document, 15, 10, "x"));
    }
}
