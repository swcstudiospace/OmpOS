using Autonogrammer.Presentation.Editor;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Diff;

/// <summary>
/// The diff review view: each hunk has its own Accept and Reject controls, accepting or rejecting one hunk changes only that
/// hunk's lines in the resulting text, and every control is named and keyboard reachable (ADE-11, XP-01).
/// </summary>
public sealed class DiffReviewViewTests
{
    private static readonly string[] HunkControlNames = ["Accept hunk 1", "Reject hunk 1", "Accept hunk 2", "Reject hunk 2"];

    /// <summary>Each hunk has its own named Accept and Reject control, and both are enabled before any decision.</summary>
    [Fact]
    public async Task EachHunkHasItsOwnNamedAcceptAndRejectControl()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = DiffHarness.Show(DiffHarness.NewReview());

            foreach (var name in HunkControlNames)
            {
                DiffHarness.Named(window, name).IsEnabled.ShouldBeTrue(name);
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Before any decision the resulting text is the original, so showing a review writes nothing.</summary>
    [Fact]
    public async Task ShowingAReviewLeavesTheResultingTextAsTheOriginal()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = DiffHarness.Show(DiffHarness.NewReview());

            DiffHarness.Result(window).ShouldBe(DiffHarness.Original);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Accepting the first hunk writes only the first hunk: the second stays pending.</summary>
    [Fact]
    public async Task AcceptingOneHunkWritesOnlyThatHunk()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var review = DiffHarness.NewReview();
            using var window = DiffHarness.Show(review);

            DiffHarness.Click(window, DiffHarness.Named(window, "Accept hunk 1"));

            DiffHarness.Result(window).ShouldBe(DiffHarness.FirstAccepted);
            review.Hunks[1].Decision.ShouldBe(HunkDecision.Pending);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Accepting only the second hunk leaves the first hunk's original line in place.</summary>
    [Fact]
    public async Task AcceptingTheSecondHunkAloneLeavesTheFirstAsOriginal()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = DiffHarness.Show(DiffHarness.NewReview());

            DiffHarness.Click(window, DiffHarness.Named(window, "Accept hunk 2"));

            DiffHarness.Result(window).ShouldBe(DiffHarness.SecondAccepted);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Rejecting one hunk after accepting both reverts only that hunk to its original lines.</summary>
    [Fact]
    public async Task RejectingOneHunkRevertsOnlyThatHunk()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = DiffHarness.Show(DiffHarness.NewReview());

            DiffHarness.Click(window, DiffHarness.Named(window, "Accept hunk 1"));
            DiffHarness.Click(window, DiffHarness.Named(window, "Accept hunk 2"));
            DiffHarness.Click(window, DiffHarness.Named(window, "Reject hunk 1"));

            DiffHarness.Result(window).ShouldBe(DiffHarness.SecondAccepted);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Accepting every hunk gives the diff's new text, and rejecting every hunk gives the original.</summary>
    [Fact]
    public async Task AcceptingEveryHunkGivesTheNewTextAndRejectingEveryHunkGivesTheOriginal()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = DiffHarness.Show(DiffHarness.NewReview());

            DiffHarness.Click(window, DiffHarness.Named(window, "Accept hunk 1"));
            DiffHarness.Click(window, DiffHarness.Named(window, "Accept hunk 2"));
            DiffHarness.Result(window).ShouldBe(DiffHarness.BothAccepted);

            DiffHarness.Click(window, DiffHarness.Named(window, "Reject hunk 1"));
            DiffHarness.Click(window, DiffHarness.Named(window, "Reject hunk 2"));
            DiffHarness.Result(window).ShouldBe(DiffHarness.Original);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A decision made on the model is drawn in the view and in the resulting text.</summary>
    [Fact]
    public async Task ADecisionMadeOnTheModelIsDrawnInTheView()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var review = DiffHarness.NewReview();
            using var window = DiffHarness.Show(review);

            review.Decide(1, HunkDecision.Accept);

            DiffHarness.Result(window).ShouldBe(DiffHarness.SecondAccepted);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Keyboard focus moves from a hunk's Accept control to its Reject control with Tab.</summary>
    [Fact]
    public async Task TabMovesFromAcceptToRejectWithinAHunk()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = DiffHarness.Show(DiffHarness.NewReview());

            DiffHarness.Named(window, "Accept hunk 1").Focus();
            DiffHarness.Press(window, Key.Tab, PhysicalKey.Tab);

            DiffHarness.Named(window, "Reject hunk 1").IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on a focused Accept control decides that hunk, so the whole review is keyboard operable.</summary>
    [Fact]
    public async Task EnterOnTheSecondHunksAcceptDecidesOnlyThatHunk()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var review = DiffHarness.NewReview();
            using var window = DiffHarness.Show(review);

            DiffHarness.Named(window, "Accept hunk 2").Focus();
            DiffHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            DiffHarness.Result(window).ShouldBe(DiffHarness.SecondAccepted);
            review.Hunks[0].Decision.ShouldBe(HunkDecision.Pending);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The hunk list, the pending count and the resulting text carry accessible names.</summary>
    [Fact]
    public async Task TheHunkListAndResultCarryAccessibleNames()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var window = DiffHarness.Show(DiffHarness.NewReview());
            var view = DiffHarness.View(window);

            AutomationProperties.GetName(view.FindControl<ItemsControl>("HunkRows")!).ShouldBe("Diff hunks");
            AutomationProperties.GetName(view.FindControl<TextBlock>("PendingText")!).ShouldBe("Pending hunks");
            AutomationProperties.GetName(view.FindControl<TextBox>("ResultBox")!).ShouldBe("Resulting text");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
