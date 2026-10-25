using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Presentation.Components;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// Native checks that close the shell rows of the Phase 21 parity inventory: the conversation list's current row and its selection callback,
/// the engine-failure banner, the host's retry and stop wiring, and the engine event fold with its diagnostic drop. The turns run through
/// the shell, so the composer, the send path and the transcript are the ones the user sees.
/// </summary>
public sealed class ParityShellGapTests
{
    private static readonly ConversationSummary First = new() { Id = "c-1", Title = "First conversation" };
    private static readonly ConversationSummary Second = new() { Id = "c-2", Title = "Second conversation" };

    /// <summary>Row 37: the current conversation is the list's selected row, and it is the only row marked as current.</summary>
    [Fact]
    public async Task CurrentConversationRowIsTheOnlyRowMarkedCurrent()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var list = ShellHarness.List(window);
            list.Conversations = [First, Second];
            list.SelectedId = Second.Id;
            ShellHarness.Settle(window);

            ShellHarness.Rows(window).SelectedItem.ShouldBe(Second);
            RowNamed(window, Second.Title).IsSelected.ShouldBeTrue("the current row is marked as selected");
            RowNamed(window, First.Title).IsSelected.ShouldBeFalse("only the current row is marked");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 38: choosing a row reports that conversation's identifier, and the chosen row becomes the current one.</summary>
    [Fact]
    public async Task ChoosingARowRaisesItsIdentifier()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var list = ShellHarness.List(window);
            list.Conversations = [First, Second];
            ShellHarness.Settle(window);

            var chosen = new List<string>();
            list.ConversationSelected += (_, e) => chosen.Add(e.Id);
            ShellHarness.Rows(window).SelectedIndex = 1;
            ShellHarness.Settle(window);

            chosen.ShouldBe([Second.Id]);
            list.SelectedId.ShouldBe(Second.Id);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 40: an engine failure that ends a turn shows its message in an assertive banner above the transcript.</summary>
    [Fact]
    public async Task EngineFailureShowsAnAssertiveBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = new ScriptedPromptChannel(new ScriptedTurn([], Fault: "model link down"));
        try
        {
            await using var session = TestAppBuilder.StartSession();
            await session.RunAsync(async () =>
            {
                var main = ParityShellHarness.CreateMain(channel);
                var window = ParityShellHarness.Show(main);
                try
                {
                    ParityShellHarness.Send(main, "hello");
                    await RunControlsHarness.UntilAsync(() => main.Session.State == SessionViewState.FatalError, "the failed turn", ct).ConfigureAwait(true);
                    ParityShellHarness.Settle(window);

                    var banner = main.FindControl<TextBlock>("ErrorBanner")!;
                    banner.IsVisible.ShouldBeTrue("the failure is shown above the transcript");
                    banner.Text.ShouldBe("The engine stream failed: model link down");
                    AutomationProperties.GetLiveSetting(banner).ShouldBe(AutomationLiveSetting.Assertive);
                }
                finally
                {
                    main.Close();
                    window.Close();
                }
            }, ct).ConfigureAwait(true);
        }
        finally
        {
            await channel.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Row 42: the host's retry sends the last prompt again through the send path after a recoverable failure, and the host's stop cancels
    /// the turn in flight there. The composer is idle before each press, as the user sees it.
    /// </summary>
    [Fact]
    public async Task HostRetryResendsTheLastPromptAndStopCancelsTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = new ScriptedPromptChannel(
            new ScriptedTurn([new TextDeltaEvent("partial"), new ErrorEvent("transient", Retryable: true)]),
            new ScriptedTurn([new TextDeltaEvent("recovered"), new CompletedEvent("end_turn")]),
            new ScriptedTurn([new TextDeltaEvent("working")], HoldUntilCancelled: true));
        try
        {
            await using var session = TestAppBuilder.StartSession();
            await session.RunAsync(async () =>
            {
                var main = ParityShellHarness.CreateMain(channel);
                var window = ParityShellHarness.Show(main);
                try
                {
                    ParityShellHarness.Send(main, "hello");
                    await RunControlsHarness.UntilAsync(() => IsIdle(main, SessionViewState.RecoverableError), "the recoverable failure", ct).ConfigureAwait(true);

                    RunControlsHarness.Click(ParityShellHarness.Transcript(main).FindControl<Button>("SessionRetryButton")!);
                    await RunControlsHarness.UntilAsync(() => IsIdle(main, SessionViewState.Success), "the retried turn", ct).ConfigureAwait(true);
                    channel.Prompts.ToArray().ShouldBe(["hello", "hello"]);

                    ParityShellHarness.Send(main, "again");
                    await RunControlsHarness.UntilAsync(() => channel.Prompts.Count == 3 && main.Session.State == SessionViewState.Streaming, "the held turn", ct).ConfigureAwait(true);

                    RunControlsHarness.Click(ParityShellHarness.Transcript(main).FindControl<Button>("SessionStopButton")!);
                    await RunControlsHarness.UntilAsync(() => channel.CancelCount == 1 && IsIdle(main, SessionViewState.Success), "the stopped turn", ct).ConfigureAwait(true);
                }
                finally
                {
                    main.Close();
                    window.Close();
                }
            }, ct).ConfigureAwait(true);
        }
        finally
        {
            await channel.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Row 44: the engine's events fold into the transcript as the turn streams, and a diagnostic is dropped: it draws nothing, is folded
    /// into no block, and the turn still completes.
    /// </summary>
    [Fact]
    public async Task EngineEventsFoldIntoTheTranscriptAndADiagnosticIsDropped()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = new ScriptedPromptChannel(new ScriptedTurn(
            [new DiagnosticEvent("slow decode"), new TextDeltaEvent("Hi"), new CompletedEvent("end_turn")]));
        try
        {
            await using var session = TestAppBuilder.StartSession();
            await session.RunAsync(async () =>
            {
                var main = ParityShellHarness.CreateMain(channel);
                var window = ParityShellHarness.Show(main);
                try
                {
                    ParityShellHarness.Send(main, "hello");
                    await RunControlsHarness.UntilAsync(() => IsIdle(main, SessionViewState.Success), "the completed turn", ct).ConfigureAwait(true);
                    ParityShellHarness.Settle(window);

                    main.Session.Tree.Blocks.OfType<MarkdownBlock>().Select(block => block.Markdown).ToArray().ShouldBe(["Hi"]);
                    main.Session.Tree.Blocks.OfType<UnknownBlock>().ShouldBeEmpty("a diagnostic is never folded into the transcript");
                    ParityShellHarness.Transcript(main).GetVisualDescendants().OfType<TextBlock>()
                        .Any(text => (text.Text ?? string.Empty).Contains("slow decode", StringComparison.Ordinal))
                        .ShouldBeFalse("a diagnostic draws nothing");
                }
                finally
                {
                    main.Close();
                    window.Close();
                }
            }, ct).ConfigureAwait(true);
        }
        finally
        {
            await channel.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Whether the shell is in the state and no send is still settling, so a press is the user's next action.</summary>
    private static bool IsIdle(MainView main, SessionViewState state) => main.Session.State == state && !main.Composer.IsStreaming;

    /// <summary>Gets the conversation row the list draws with the given title.</summary>
    private static ListBoxItem RowNamed(MainWindow window, string title) =>
        ShellHarness.Rows(window).GetVisualDescendants().OfType<ListBoxItem>()
            .Single(row => string.Equals(AutomationProperties.GetName(row), title, StringComparison.Ordinal));
}
