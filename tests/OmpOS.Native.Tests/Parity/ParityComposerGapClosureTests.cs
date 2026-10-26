using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Native.Tests.Send;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Presentation;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Parity;

/// <summary>
/// Native checks that close the composer rows of the Phase 21 parity inventory: the draft rules, the send and stop controls, and the
/// key rules while a response streams. Each drives the shell's composer through the same harness the composer tests use.
/// </summary>
public sealed class ParityComposerGapClosureTests
{
    private const string Hello = "hello";
    private const string CancelMethod = "session/cancel";

    /// <summary>Row 31: a draft typed while a response streams stays in the composer when Enter is pressed, and no refusal is shown.</summary>
    [Fact]
    public async Task ADraftTypedWhileAResponseStreamsIsKeptOnEnter()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            var composer = ShellHarness.ComposerModelOf(window);
            composer.IsStreaming = true;

            ShellHarness.Prompt(window).Text = "still typing";
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            composer.Draft.ShouldBe("still typing");
            ShellHarness.Prompt(window).Text.ShouldBe("still typing");
            ShellHarness.Status(window).IsVisible.ShouldBeFalse("a send made while a response streams is neither sent nor refused");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Row 32: while a turn streams, Shift+Enter neither sends a new prompt nor stops the turn. Escape asks first: it raises the stop
    /// confirmation bar and stops nothing until Stop agent is pressed, and that press stops the turn through the host.
    /// </summary>
    [Fact]
    public async Task ShiftEnterWhileStreamingNeitherSendsNorStopsAndEscapeStopsTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(holdPrompt: true);
        var engine = ComposedOver(agent);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = ShowOver(workspace, engine);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.ShownWindow;

            ShellHarness.Prompt(window).Text = "long task";
            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => agent.PromptTexts.Count == 1, "the held prompt", ct).ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "more";
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter, shift: true);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            agent.PromptTexts.Count.ShouldBe(1, "Shift+Enter sends nothing");
            agent.Methods.ShouldNotContain(CancelMethod, StringComparer.Ordinal, "Shift+Enter does not stop the turn");
            shell.Composer.IsStreaming.ShouldBeTrue();

            ShellHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            var stopBar = ShellTree.Named<Border>(shell.Main, "StopConfirmBar")!;
            stopBar.IsVisible.ShouldBeTrue("Escape while a run is active asks before it stops the turn");
            agent.Methods.ShouldNotContain(CancelMethod, StringComparer.Ordinal, "Escape alone does not stop the turn");
            shell.Composer.IsStreaming.ShouldBeTrue();

            RunControlsHarness.Click(ShellTree.Named<Button>(shell.Main, "StopAgentButton")!);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the stopped turn", ct).ConfigureAwait(true);

            agent.Methods.Count(method => string.Equals(method, CancelMethod, StringComparison.Ordinal)).ShouldBe(1);
            shell.Composer.IsStreaming.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Row 33: the send button sends nothing for a whitespace-only draft, and it does send a draft with text.</summary>
    [Fact]
    public async Task TheSendButtonDoesNotSendAWhitespaceOnlyDraft()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            var composer = ShellHarness.ComposerModelOf(window);

            ShellHarness.Prompt(window).Text = "   ";
            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).IsVisible.ShouldBeFalse("a whitespace-only draft is not sent, so the host never refuses it");
            composer.Draft.ShouldBe("   ");

            ShellHarness.Prompt(window).Text = Hello;
            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).Text.ShouldBe(MainView.RefusedSendStatus, "a draft with text reaches the host, which refuses it here");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 35: Enter on a whitespace-only draft is a no-op: the draft stays and nothing is sent or refused.</summary>
    [Fact]
    public async Task EnterOnAWhitespaceOnlyDraftIsANoOp()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            var composer = ShellHarness.ComposerModelOf(window);

            ShellHarness.Prompt(window).Text = "   ";
            ShellHarness.Prompt(window).Focus();
            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).IsVisible.ShouldBeFalse("a whitespace-only draft is not sent");
            ShellHarness.Prompt(window).Text.ShouldBe("   ");
            composer.Draft.ShouldBe("   ");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row 34: the composer's stop button cancels the streaming turn through the host, and the turn ends.</summary>
    [Fact]
    public async Task TheComposerStopButtonCancelsTheStreamingTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(holdPrompt: true);
        var engine = ComposedOver(agent);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = ShowOver(workspace, engine);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.ShownWindow;

            ShellHarness.Prompt(window).Text = "long task";
            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => agent.PromptTexts.Count == 1, "the held prompt", ct).ConfigureAwait(true);
            shell.Composer.IsStreaming.ShouldBeTrue();

            RunControlsHarness.Click(ShellHarness.Stop(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the cancelled turn", ct).ConfigureAwait(true);

            agent.Methods.Count(method => string.Equals(method, CancelMethod, StringComparison.Ordinal)).ShouldBe(1);
            shell.Composer.IsStreaming.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Row 41: a send the host refuses from the send control keeps the draft and shows the refusal on the status line.</summary>
    [Fact]
    public async Task ARefusedSendFromTheSendControlKeepsTheDraftAndShowsTheRefusal()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            var composer = ShellHarness.ComposerModelOf(window);

            ShellHarness.Prompt(window).Text = Hello;
            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);

            ShellHarness.Status(window).IsVisible.ShouldBeTrue("the refusal is shown on the status line");
            ShellHarness.Status(window).Text.ShouldBe(MainView.RefusedSendStatus);
            composer.Draft.ShouldBe(Hello);
            ShellHarness.Prompt(window).Text.ShouldBe(Hello);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static Lazy<EngineComposition> ComposedOver(ScriptedAgent agent) =>
        new(() => EngineComposition.Compose([], _ => new ScriptedAgentTransport(agent.Respond)));

    /// <summary>Shows a shell whose one session sends through the composed engine. Disposing the shell closes the window and the engine.</summary>
    private static ParityShell ShowOver(TempWorkspace workspace, Lazy<EngineComposition> engine)
    {
        var shell = new ParityShell(() => new ComposedEngineChannel(engine));
        shell.Window = ShellHarness.Show(shell.Fixture, MotionPreference.System, workspaceRoot: workspace.Root);
        return shell;
    }

    /// <summary>The send path of a session over the composed engine: the engine's host submits each prompt and cancels the turn in flight.</summary>
    /// <param name="engine">The lazily composed engine. Nothing is composed until the first send.</param>
    private sealed class ComposedEngineChannel(Lazy<EngineComposition> engine) : IPromptChannel
    {
        /// <inheritdoc />
        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) =>
            engine.Value.Host.SubmitPromptAsync(prompt, new PromptOptions(), cancellationToken);

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) => engine.Value.Host.CancelAsync(cancellationToken);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (engine.IsValueCreated)
            {
                await engine.Value.Host.DisposeAsync().ConfigureAwait(true);
            }
        }
    }
}
