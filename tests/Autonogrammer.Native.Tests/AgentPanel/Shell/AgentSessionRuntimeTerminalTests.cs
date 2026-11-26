using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Steering;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.Terminal;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Terminal;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// A persisted terminal record in the session runtime (Phase 81): the runtime is retired, so it never calls the starter, holds no terminal
/// view model and offers no steering or send surface. The fake starter is the never-called witness: omp is never launched. Live-terminal
/// lifecycle (omp start/stop, close, dispose) is pinned absent here; the shell-level retired pins are in AgentPanelTerminalSessionTests.
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

            terminal.IsRetiredTerminal.ShouldBeTrue();
            terminal.Kind.ShouldBe(SessionKind.Terminal, "the persisted kind is kept for listing");
            terminal.Terminal.ShouldBeNull("a retired record has no terminal view model");
            terminal.Runs.IsRunActive.ShouldBeFalse();
            starter.Sizes.ShouldBeEmpty("omp is never launched for a retired record");
            starter.Sessions.ShouldBeEmpty();
            chat.Kind.ShouldBe(SessionKind.Chat);
            chat.IsRetiredTerminal.ShouldBeFalse();
            chat.Terminal.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A retired terminal offers no steering: a submit finds no run and the channel is never asked.</summary>
    [Fact]
    public async Task SteeringIsOfferedWhileOmpRuns()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel();
            var runtime = Create(new FakeOmpTerminalStarter(), channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var steering = runtime.ViewModel.Steering;

            (await steering.SubmitAsync("turn left", SteeringMode.Steer, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.NoActiveRun);
            (await steering.SubmitAsync("then summarise", SteeringMode.FollowUp, token).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.NoActiveRun);
            channel.Sent.ShouldBeEmpty("there is no steering surface for a retired record");
            steering.History.ShouldBeEmpty();
            runtime.Runs.IsRunActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A retired terminal has no prompt: a send is not taken and the status line says sending is unavailable.</summary>
    [Fact]
    public async Task ASendWithOmpStoppedSaysSoAndIsNotTaken()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);

            (await runtime.Composer.SendFromAsync("hello").ConfigureAwait(true)).ShouldBeFalse();

            runtime.StatusText.ShouldBe(MainView.RefusedSendStatus);
            runtime.Composer.IsStreaming.ShouldBeFalse();
            starter.Sizes.ShouldBeEmpty();
            starter.Sessions.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Disposing the runtime of a retired record starts nothing: the starter is never called and disposal is safe to repeat.</summary>
    [Fact]
    public async Task DisposingTheRuntimeDisposesTheTerminalSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel());

            await runtime.DisposeAsync().ConfigureAwait(true);
            await runtime.DisposeAsync().ConfigureAwait(true);

            starter.Sizes.ShouldBeEmpty("omp is never launched for a retired record");
            starter.Sessions.ShouldBeEmpty("a retired record has no terminal session to dispose");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored closed stays retired: no terminal is created and reopening it starts nothing.</summary>
    [Fact]
    public async Task ATerminalRestoredClosedDoesNotAutoStartAndReopeningStartsIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel(), closed: true);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.ViewModel.IsClosed.ShouldBeTrue();
            runtime.IsRetiredTerminal.ShouldBeTrue();
            runtime.Terminal.ShouldBeNull();
            starter.Sizes.ShouldBeEmpty();

            runtime.ViewModel.MarkReopened();

            runtime.IsRetiredTerminal.ShouldBeTrue("reopening a retired record does not revive it");
            runtime.Terminal.ShouldBeNull();
            starter.Sizes.ShouldBeEmpty("reopening starts no omp for a retired record");
            starter.Sessions.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored lost stays retired: no terminal is created and resuming it starts nothing.</summary>
    [Fact]
    public async Task ATerminalRestoredLostDoesNotAutoStartAndResumingStartsIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var starter = new FakeOmpTerminalStarter();
            var runtime = Create(starter, new FakeSteeringChannel(), lost: true);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.ViewModel.IsLost.ShouldBeTrue();
            runtime.IsRetiredTerminal.ShouldBeTrue();
            runtime.Terminal.ShouldBeNull();
            starter.Sizes.ShouldBeEmpty();

            runtime.ViewModel.ApplyRow(runtime.ViewModel.Row with { Status = AgentRowStatus.Running, IsLost = false });

            runtime.IsRetiredTerminal.ShouldBeTrue("resuming a retired record does not revive it");
            runtime.Terminal.ShouldBeNull();
            starter.Sizes.ShouldBeEmpty("resuming starts no omp for a retired record");
            starter.Sessions.ShouldBeEmpty();
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
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "autonogrammer-shell-tests", sessionId),
            },
            Stages = new ScriptedStageSource(),
            Context = new ScriptedTaskContext(null),
            Steering = channel,
            TerminalStarter = starter,
        });
    }
}
