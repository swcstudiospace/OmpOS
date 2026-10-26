using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// Native checks that close the shell rows of the Phase 21 parity inventory: the agent rail's current row and its selection, the
/// engine-failure banner, the host's retry and stop wiring, and the engine event fold with its diagnostic drop. The turns run through
/// the shell, so the composer, the send path and the transcript are the ones the user sees.
/// </summary>
public sealed class ParityShellGapTests
{
    private const string FirstId = "c-1";
    private const string FirstTitle = "First conversation";
    private const string SecondId = "c-2";
    private const string SecondTitle = "Second conversation";

    /// <summary>Row 37: the current agent is the rail's selected row, and it is the only row marked as current.</summary>
    [Fact]
    public async Task CurrentConversationRowIsTheOnlyRowMarkedCurrent()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession(FirstId, FirstTitle);
            fixture.AddSession(SecondId, SecondTitle);
            fixture.Panel.SelectSession(SecondId);
            var window = ShellHarness.Show(fixture);

            ShellHarness.Rows(window).SelectedItem.ShouldBeOfType<AgentRow>().Id.ShouldBe(SecondId);
            RowNamed(window, SecondTitle).IsSelected.ShouldBeTrue("the current row is marked as selected");
            RowNamed(window, FirstTitle).IsSelected.ShouldBeFalse("only the current row is marked");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 38: choosing a row reports that agent's identifier, and the chosen row becomes the current one.</summary>
    [Fact]
    public async Task ChoosingARowRaisesItsIdentifier()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession(FirstId, FirstTitle);
            fixture.AddSession(SecondId, SecondTitle);
            var window = ShellHarness.Show(fixture);

            var chosen = new List<string?>();
            fixture.Catalog.SelectionChanged += (_, e) => chosen.Add(e.SessionId);
            ShellHarness.Rows(window).SelectedItem = fixture.Catalog.Rows.Single(row => string.Equals(row.Id, SecondId, StringComparison.Ordinal));
            ShellHarness.Settle(window);

            chosen.ShouldBe([SecondId]);
            fixture.Catalog.SelectedId.ShouldBe(SecondId);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 40: an engine failure that ends a turn shows its message in an assertive banner above the transcript.</summary>
    [Fact]
    public async Task EngineFailureShowsAnAssertiveBanner()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = new ScriptedPromptChannel(new ScriptedTurn([], Fault: "model link down"));
        await using var channelScope = channel.ConfigureAwait(true);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var shell = ParityShellHarness.CreateMain(channel);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = ParityShellHarness.Show(shell);

            ParityShellHarness.Send(shell, "hello");
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.RecoverableError, "the failed turn", ct).ConfigureAwait(true);
            ParityShellHarness.Settle(window);

            var banner = shell.Main.FindControl<TextBlock>("ErrorBanner")!;
            banner.IsVisible.ShouldBeTrue("the failure is shown above the transcript");
            banner.Text.ShouldBe("This turn failed: model link down");
            AutomationProperties.GetLiveSetting(banner).ShouldBe(AutomationLiveSetting.Assertive);
        }, ct).ConfigureAwait(true);
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
        await using var channelScope = channel.ConfigureAwait(true);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var shell = ParityShellHarness.CreateMain(channel);
            await using var shellScope = shell.ConfigureAwait(true);
            ParityShellHarness.Show(shell);

            ParityShellHarness.Send(shell, "hello");
            await RunControlsHarness.UntilAsync(() => IsIdle(shell, SessionViewState.RecoverableError), "the recoverable failure", ct).ConfigureAwait(true);

            RunControlsHarness.Click(ParityShellHarness.Transcript(shell).FindControl<Button>("SessionRetryButton")!);
            await RunControlsHarness.UntilAsync(() => IsIdle(shell, SessionViewState.Success), "the retried turn", ct).ConfigureAwait(true);
            channel.Prompts.ToArray().ShouldBe(["hello", "hello"]);

            ParityShellHarness.Send(shell, "again");
            await RunControlsHarness.UntilAsync(() => channel.Prompts.Count == 3 && shell.Session.State == SessionViewState.Streaming, "the held turn", ct).ConfigureAwait(true);

            RunControlsHarness.Click(ParityShellHarness.Transcript(shell).FindControl<Button>("SessionStopButton")!);
            await RunControlsHarness.UntilAsync(() => channel.CancelCount == 1 && IsIdle(shell, SessionViewState.Success), "the stopped turn", ct).ConfigureAwait(true);
        }, ct).ConfigureAwait(true);
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
        await using var channelScope = channel.ConfigureAwait(true);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var shell = ParityShellHarness.CreateMain(channel);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = ParityShellHarness.Show(shell);

            ParityShellHarness.Send(shell, "hello");
            await RunControlsHarness.UntilAsync(() => IsIdle(shell, SessionViewState.Success), "the completed turn", ct).ConfigureAwait(true);
            ParityShellHarness.Settle(window);

            shell.Session.Tree.Blocks.OfType<MarkdownBlock>().Select(block => block.Markdown).ToArray().ShouldBe(["Hi"]);
            shell.Session.Tree.Blocks.OfType<UnknownBlock>().ShouldBeEmpty("a diagnostic is never folded into the transcript");
            ParityShellHarness.Transcript(shell).GetVisualDescendants().OfType<TextBlock>()
                .Any(text => (text.Text ?? string.Empty).Contains("slow decode", StringComparison.Ordinal))
                .ShouldBeFalse("a diagnostic draws nothing");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Whether the shell is in the state and no send is still settling, so a press is the user's next action.</summary>
    private static bool IsIdle(ParityShell shell, SessionViewState state) => shell.Session.State == state && !shell.Composer.IsStreaming;

    /// <summary>Gets the agent row the rail draws with the given title. A row's accessible name starts with its title, then its status.</summary>
    private static ListBoxItem RowNamed(MainWindow window, string title) =>
        ShellHarness.Rows(window).GetVisualDescendants().OfType<ListBoxItem>()
            .Single(row => (AutomationProperties.GetName(row) ?? string.Empty).StartsWith(title + ", ", StringComparison.Ordinal));
}
