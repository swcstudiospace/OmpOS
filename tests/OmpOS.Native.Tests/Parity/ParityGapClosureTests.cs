using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Contracts.Ui;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Views;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// Native checks that close Phase 21 parity rows the Avalonia head already renders: the transcript's loading and empty states, the
/// conversation list's empty prompt, the skip link's focus target, and the error block's by-design absence of an action.
/// </summary>
public sealed class ParityGapClosureTests
{
    /// <summary>The loading copy the transcript draws while history loads.</summary>
    private const string LoadingHeading = "Loading conversation history…";

    /// <summary>Row 21: while history loads, the transcript states that the conversation history is loading.</summary>
    [Fact]
    public async Task LoadingHistoryDrawsItsPlaceholderInTheTranscript()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            var view = new SessionView { DataContext = model };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            model.State.ShouldBe(SessionViewState.LoadingHistory);
            var heading = view.FindControl<TextBlock>("StateHeading")!;
            heading.IsVisible.ShouldBeTrue();
            heading.Text.ShouldBe(LoadingHeading);
            view.FindControl<TextBlock>("StateBody")!.IsVisible.ShouldBeFalse("the loading copy has no body line");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 22: a transcript with no messages draws the empty prompt.</summary>
    [Fact]
    public async Task AnEmptyTranscriptDrawsItsPrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkHistoryLoaded();
            var view = new SessionView { DataContext = model };
            var window = new Window { Content = view };
            window.Show();
            ShellHarness.Settle(window);

            model.State.ShouldBe(SessionViewState.Empty);
            view.FindControl<TextBlock>("StateHeading")!.Text.ShouldBe("No messages yet.");
            view.FindControl<TextBlock>("StateBody")!.Text.ShouldBe("Send a message to begin.");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 36: with no sessions, the shell's agent rail draws its empty prompt and no rows.</summary>
    [Fact]
    public async Task ShellWithNoSessionsDrawsTheEmptyConversationListPrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var rail = (AgentRailView)ShellHarness.MainViewOf(window).FindControl<ContentControl>("RailHost")!.Content!;

            ShellHarness.Rows(window).ItemCount.ShouldBe(0);
            rail.FindControl<TextBlock>("StateHeading")!.Text.ShouldBe("No agents yet.");
            rail.FindControl<TextBlock>("StateBody")!.Text.ShouldBe("Start an agent to work on a task.");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 39: activating the skip link moves keyboard focus to the transcript region.</summary>
    [Fact]
    public async Task TheSkipLinkMovesFocusToTheTranscript()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Rows(window).Focus();

            ShellHarness.SkipLink(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            ShellHarness.Scroll(window).IsFocused.ShouldBeTrue("the skip link lands on the transcript");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Row 11, by design: the error block draws its message and no action control, retry included, whether or not the error is
    /// retryable. The block's own code says retry is not drawn here.
    /// </summary>
    [Fact]
    public async Task ErrorBlockDrawsNoActionByDesign()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            bool[] retryableCases = [false, true];
            foreach (var retryable in retryableCases)
            {
                var window = new Window { Content = ViewMapping.CreateBlock(new ErrorBlock { Id = "e-1", Message = "boom", Retryable = retryable }) };
                window.Show();
                ShellHarness.Settle(window);

                window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)
                    .ShouldContain("boom", StringComparer.Ordinal);
                window.GetVisualDescendants().OfType<Button>().ShouldBeEmpty("the error block offers no action by design");
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
