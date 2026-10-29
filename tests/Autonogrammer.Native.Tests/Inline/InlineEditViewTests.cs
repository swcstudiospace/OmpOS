using Autonogrammer.Presentation.Editor;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.InlineEditing;

/// <summary>
/// The inline edit view: a proposal is shown as a reviewable change and changes nothing until Accept, Reject leaves the text as
/// it was, Undo restores the original, and every control is named and keyboard reachable (ADE-12, XP-01).
/// </summary>
public sealed class InlineEditViewTests
{
    private static readonly string[] ControlNames =
    [
        "Edit instruction", "Selected text", "Proposed replacement", "Propose replacement",
        "Accept edit", "Reject edit", "Undo edit", "Edit status",
    ];

    /// <summary>The captured selection and instruction are shown, and every control carries an accessible name.</summary>
    [Fact]
    public async Task TheSelectionInstructionAndEveryControlAreNamed()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = InlineHarness.Show(InlineHarness.NewEdit(InlineHarness.NewDocument()));

            InlineHarness.View(window).FindControl<TextBlock>("InstructionText")!.Text.ShouldBe(InlineHarness.Instruction);
            InlineHarness.View(window).FindControl<TextBlock>("SelectionText")!.Text.ShouldBe("brown");
            foreach (var name in ControlNames)
            {
                InlineHarness.Named<Control>(window, name).ShouldNotBeNull();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A proposal is shown as the removed selection and the added proposal, and the buffer is unchanged until Accept.</summary>
    [Fact]
    public async Task AProposalIsShownAsAChangeAndLeavesTheBufferUnchanged()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var document = InlineHarness.NewDocument();
            var edit = InlineHarness.NewEdit(document);
            using var window = InlineHarness.Show(edit);

            InlineHarness.Type(window, "red");
            InlineHarness.ChangePanel(window).IsVisible.ShouldBeFalse("nothing is reviewable before a proposal");
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Propose replacement"));

            edit.Status.ShouldBe(InlineEditStatus.Proposed);
            InlineHarness.ChangePanel(window).IsVisible.ShouldBeTrue();
            (InlineHarness.View(window).FindControl<TextBlock>("RemovedText")!.Text ?? string.Empty).ShouldContain("brown");
            (InlineHarness.View(window).FindControl<TextBlock>("AddedText")!.Text ?? string.Empty).ShouldContain("red");
            document.Text.ShouldBe(InlineHarness.Original);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Accept replaces exactly the selected range with the proposal.</summary>
    [Fact]
    public async Task AcceptReplacesOnlyTheSelectedRange()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var document = InlineHarness.NewDocument();
            using var window = InlineHarness.Show(InlineHarness.NewEdit(document));

            InlineHarness.Type(window, "red");
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Propose replacement"));
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Accept edit"));

            document.Text.ShouldBe(InlineHarness.RedAccepted);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reject leaves the buffer exactly as it was, and the rejected edit can no longer be accepted.</summary>
    [Fact]
    public async Task RejectLeavesTheTextUnchanged()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var document = InlineHarness.NewDocument();
            var edit = InlineHarness.NewEdit(document);
            using var window = InlineHarness.Show(edit);

            InlineHarness.Type(window, "red");
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Propose replacement"));
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Reject edit"));

            document.Text.ShouldBe(InlineHarness.Original);
            edit.Status.ShouldBe(InlineEditStatus.Rejected);
            InlineHarness.Named<Button>(window, "Accept edit").IsEnabled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Undo after an accept restores the original selected text.</summary>
    [Fact]
    public async Task UndoRestoresTheOriginalText()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var document = InlineHarness.NewDocument();
            using var window = InlineHarness.Show(InlineHarness.NewEdit(document));

            InlineHarness.Type(window, "red");
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Propose replacement"));
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Accept edit"));
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Undo edit"));

            document.Text.ShouldBe(InlineHarness.Original);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Undo is offered only after an accept: before it, the control is disabled.</summary>
    [Fact]
    public async Task UndoIsDisabledUntilTheProposalIsAccepted()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = InlineHarness.Show(InlineHarness.NewEdit(InlineHarness.NewDocument()));

            InlineHarness.Named<Button>(window, "Undo edit").IsEnabled.ShouldBeFalse();

            InlineHarness.Type(window, "red");
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Propose replacement"));
            InlineHarness.Named<Button>(window, "Undo edit").IsEnabled.ShouldBeFalse();

            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Accept edit"));
            InlineHarness.Named<Button>(window, "Undo edit").IsEnabled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>When the buffer changed after capture, Accept changes nothing and the edit stays proposed.</summary>
    [Fact]
    public async Task AcceptAfterTheBufferChangedLeavesTheNewerTextAlone()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var document = InlineHarness.NewDocument();
            var edit = InlineHarness.NewEdit(document);
            using var window = InlineHarness.Show(edit);

            InlineHarness.Type(window, "red");
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Propose replacement"));
            document.Replace(InlineHarness.SelectionStart, InlineHarness.SelectionLength, "blue");
            InlineHarness.Click(window, InlineHarness.Named<Button>(window, "Accept edit"));

            document.Text.ShouldBe("The quick blue fox.\n");
            edit.Status.ShouldBe(InlineEditStatus.Proposed);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Tab moves from the proposal input to Propose, then to Accept and Reject once a proposal is pending.</summary>
    [Fact]
    public async Task TabMovesFromTheProposalInputThroughTheDecisions()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = InlineHarness.Show(InlineHarness.NewEdit(InlineHarness.NewDocument()));

            InlineHarness.Proposal(window).Focus();
            InlineHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            InlineHarness.Named<Button>(window, "Propose replacement").IsFocused.ShouldBeTrue("Propose follows the input");

            InlineHarness.Type(window, "red");
            InlineHarness.Named<Button>(window, "Propose replacement").Focus();
            InlineHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            InlineHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            InlineHarness.Named<Button>(window, "Accept edit").IsFocused.ShouldBeTrue("Accept follows Propose once proposed");

            InlineHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            InlineHarness.Named<Button>(window, "Reject edit").IsFocused.ShouldBeTrue("Reject follows Accept");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on the focused Accept control applies the proposal from the keyboard.</summary>
    [Fact]
    public async Task EnterOnAcceptAppliesTheProposalFromTheKeyboard()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var document = InlineHarness.NewDocument();
            using var window = InlineHarness.Show(InlineHarness.NewEdit(document));

            InlineHarness.Type(window, "red");
            InlineHarness.Named<Button>(window, "Propose replacement").Focus();
            InlineHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            InlineHarness.Named<Button>(window, "Accept edit").Focus();
            InlineHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            document.Text.ShouldBe(InlineHarness.RedAccepted);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
