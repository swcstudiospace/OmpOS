using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.Terminal;
using OmpOS.Native.Views.Sessions;
using OmpOS.Native.Views.Terminal;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>
/// The runtime of an omp terminal session: it has a terminal and no prompt, steering is offered while omp runs, and closing or disposing the
/// session ends omp. Nothing here starts a real omp: the terminal starts through a fake starter.
/// </summary>
public sealed class AgentSessionRuntimeTerminalTests
{
    /// <summary>A starter makes a terminal runtime; a runtime without one is a chat. The terminal is not started by the runtime: the view starts it.</summary>
    [Fact]
    public async Task AStarterMakesATerminalRuntimeThatStartsNothingByItself()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            var terminal = Create(starter, new FakeSteeringChannel());
            await using var terminalScope = terminal.ConfigureAwait(true);
            var chat = Create(starter: null, channel: null, sessionId: "session-2");
            await using var chatScope = chat.ConfigureAwait(true);

            terminal.Kind.ShouldBe(SessionKind.Terminal);
            var model = terminal.Terminal.ShouldNotBeNull();
            model.State.ShouldBe(OmpTerminalState.Idle);
            model.KeepRunningWhenDetached.ShouldBeTrue("a hidden tab or session must not stop omp");
            starter.Sizes.ShouldBeEmpty();
            chat.Kind.ShouldBe(SessionKind.Chat);
            chat.Terminal.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Steering is offered exactly while omp runs: a steer reaches the channel, and after omp exits a steer is told there is no run.</summary>
    [Fact]
    public async Task SteeringIsOfferedWhileOmpRuns()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var starter = new FakeOmpTerminalStarter();
            var channel = new FakeSteeringChannel();
            var runtime = Create(starter, channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var steering = runtime.ViewModel.Steering;

            (await steering.SubmitAsync("early", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.NoActiveRun);
            channel.Sent.ShouldBeEmpty("omp is not running yet");

            var model = runtime.Terminal.ShouldNotBeNull();
            await model.StartAsync().ConfigureAwait(true);

            runtime.Runs.IsRunActive.ShouldBeTrue();
            (await steering.SubmitAsync("turn left", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);
            (await steering.SubmitAsync("then summarise", SteeringMode.FollowUp, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);
            channel.Sent.Select(directive => (directive.Mode, directive.Text)).ShouldBe(
                [(SteeringDirectiveMode.Steer, "turn left"), (SteeringDirectiveMode.FollowUp, "then summarise")]);

            starter.Sessions.Single().Exit(0);
            await ShellWait.UntilAsync(() => model.State == OmpTerminalState.Exited, "omp to exit", token).ConfigureAwait(true);

            runtime.Runs.IsRunActive.ShouldBeFalse();
            (await steering.SubmitAsync("late", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.NoActiveRun);
            channel.Sent.Count.ShouldBe(2);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal has no prompt: a send with omp stopped says so on the status line and the draft is not taken.</summary>
    [Fact]
    public async Task ASendWithOmpStoppedSaysSoAndIsNotTaken()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(new FakeOmpTerminalStarter(), new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);

            (await runtime.Composer.SendFromAsync("hello").ConfigureAwait(true)).ShouldBeFalse();

            runtime.StatusText.ShouldBe(AgentPanelCopy.TerminalNotRunning);
            runtime.Composer.IsStreaming.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing the session ends omp and disposes its session; reopening starts omp again in the same terminal.</summary>
    [Fact]
    public async Task ClosingEndsOmpAndReopeningStartsItAgain()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var model = runtime.Terminal.ShouldNotBeNull();
            await model.StartAsync().ConfigureAwait(true);
            var first = starter.Sessions.Single();

            runtime.ViewModel.MarkClosed();
            await ShellWait.UntilAsync(() => model.State == OmpTerminalState.Idle && first.Disposals == 1, "omp to stop", token).ConfigureAwait(true);

            runtime.Runs.IsRunActive.ShouldBeFalse();

            runtime.ViewModel.MarkReopened();
            await ShellWait.UntilAsync(() => model.State == OmpTerminalState.Running, "omp to start again", token).ConfigureAwait(true);

            starter.Sessions.Count.ShouldBe(2, "reopening starts a new omp, once");
            runtime.Runs.IsRunActive.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The terminal's own Close control closes the session like any other close.</summary>
    [Fact]
    public async Task TheTerminalsCloseControlClosesTheSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var runtime = Create(new FakeOmpTerminalStarter(), new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var closeRequests = 0;
            runtime.ViewModel.CloseRequested += (_, _) => closeRequests++;
            var model = runtime.Terminal.ShouldNotBeNull();
            await model.StartAsync().ConfigureAwait(true);

            model.CloseCommand.Execute(null);
            await ShellWait.UntilAsync(() => closeRequests == 1, "the session to ask to close", token).ConfigureAwait(true);

            model.State.ShouldBe(OmpTerminalState.Idle);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Disposing the runtime stops omp and disposes its session exactly once.</summary>
    [Fact]
    public async Task DisposingTheRuntimeDisposesTheTerminalSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel());
            var model = runtime.Terminal.ShouldNotBeNull();
            await model.StartAsync().ConfigureAwait(true);
            var session = starter.Sessions.Single();

            await runtime.DisposeAsync().ConfigureAwait(true);
            await runtime.DisposeAsync().ConfigureAwait(true);

            session.Disposals.ShouldBe(1, "the terminal session is disposed once, by the runtime");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored closed does not let its view start omp; reopening it starts omp once.</summary>
    [Fact]
    public async Task ATerminalRestoredClosedDoesNotAutoStartAndReopeningStartsIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel(), closed: true);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var model = runtime.Terminal.ShouldNotBeNull();

            runtime.ViewModel.IsClosed.ShouldBeTrue();
            model.AutoStart.ShouldBeFalse("a closed session never starts omp by being shown");
            starter.Sizes.ShouldBeEmpty();

            runtime.ViewModel.MarkReopened();
            await ShellWait.UntilAsync(() => model.State == OmpTerminalState.Running, "omp to start after the reopen", token).ConfigureAwait(true);

            model.AutoStart.ShouldBeTrue();
            starter.Sessions.Count.ShouldBe(1, "reopening starts omp once");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored lost does not let its view start omp; once the row is no longer lost omp starts.</summary>
    [Fact]
    public async Task ATerminalRestoredLostDoesNotAutoStartAndResumingStartsIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel(), lost: true);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var model = runtime.Terminal.ShouldNotBeNull();

            runtime.ViewModel.IsLost.ShouldBeTrue();
            model.AutoStart.ShouldBeFalse("a lost session never starts omp by being shown");
            starter.Sizes.ShouldBeEmpty();

            runtime.ViewModel.ApplyRow(runtime.ViewModel.Row with { Status = AgentRowStatus.Running, IsLost = false });
            await ShellWait.UntilAsync(() => model.State == OmpTerminalState.Running, "omp to start after the resume", token).ConfigureAwait(true);

            model.AutoStart.ShouldBeTrue();
            starter.Sessions.Count.ShouldBe(1, "resuming starts omp once");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(IOmpTerminalStarter? starter, ISteeringChannel? channel, string sessionId = "session-1", bool closed = false, bool lost = false)
    {
        var now = DateTimeOffset.UnixEpoch;
        var catalog = new FakeAgentCatalog();
        catalog.Put(new SessionSummary
        {
            Id = sessionId,
            Title = $"Agent {sessionId}",
            Lifecycle = closed ? SessionLifecycle.Paused : lost ? SessionLifecycle.Lost : SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
            Kind = starter is null ? SessionKind.Chat : SessionKind.Terminal,
        });
        using var model = new SessionCatalogModel(catalog, new ShellClock(), new SessionCatalogOptions { IsClosed = _ => closed });
        model.Refresh();
        var row = model.Sessions.Single();
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = row,
            Prompts = new UnconnectedPromptChannel(),
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = sessionId,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            Steering = channel,
            TerminalStarter = starter,
        });
    }
}
