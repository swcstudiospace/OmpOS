using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Steering;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// A persisted terminal record in the session runtime (Phase 81): the runtime is retired. No starter input exists any more, so it can start no
/// terminal engine and offers no steering or send surface. The shell-level retired pins are in AgentPanelTerminalSessionTests.
/// </summary>
public sealed class AgentSessionRuntimeTerminalTests
{
    /// <summary>A persisted Terminal row makes a retired runtime, and a chat row a chat runtime; neither runs anything by itself.</summary>
    [Fact]
    public async Task APersistedTerminalRuntimeStartsNothingByItself()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var terminal = Create(SessionKind.Terminal, new FakeSteeringChannel());
            await using var terminalScope = terminal.ConfigureAwait(true);
            var chat = Create(SessionKind.Chat, channel: null, sessionId: "session-2");
            await using var chatScope = chat.ConfigureAwait(true);

            terminal.IsRetiredTerminal.ShouldBeTrue();
            terminal.Kind.ShouldBe(SessionKind.Terminal, "the persisted kind is kept for listing");
            terminal.Runs.IsRunActive.ShouldBeFalse();
            chat.Kind.ShouldBe(SessionKind.Chat);
            chat.IsRetiredTerminal.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A retired terminal offers no steering: a submit finds no run and the channel is never asked.</summary>
    [Fact]
    public async Task SteeringIsNotOfferedForARetiredTerminal()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new FakeSteeringChannel();
            var runtime = Create(SessionKind.Terminal, channel);
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
    public async Task ASendFromARetiredTerminalIsRefused()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(SessionKind.Terminal, new FakeSteeringChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);

            (await runtime.Composer.SendFromAsync("hello").ConfigureAwait(true)).ShouldBeFalse();

            runtime.StatusText.ShouldBe(MainView.RefusedSendStatus);
            runtime.Composer.IsStreaming.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Disposing the runtime of a retired record starts nothing, and disposal is safe to repeat.</summary>
    [Fact]
    public async Task DisposingARetiredRuntimeIsSafeToRepeat()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(SessionKind.Terminal, new FakeSteeringChannel());

            await runtime.DisposeAsync().ConfigureAwait(true);
            await runtime.DisposeAsync().ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored closed stays retired, and reopening it does not revive it.</summary>
    [Fact]
    public async Task ATerminalRestoredClosedStaysRetiredWhenReopened()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(SessionKind.Terminal, new FakeSteeringChannel(), closed: true);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.ViewModel.IsClosed.ShouldBeTrue();
            runtime.IsRetiredTerminal.ShouldBeTrue();

            runtime.ViewModel.MarkReopened();

            runtime.IsRetiredTerminal.ShouldBeTrue("reopening a retired record does not revive it");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A terminal session restored lost stays retired, and resuming it does not revive it.</summary>
    [Fact]
    public async Task ATerminalRestoredLostStaysRetiredWhenResumed()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(SessionKind.Terminal, new FakeSteeringChannel(), lost: true);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.ViewModel.IsLost.ShouldBeTrue();
            runtime.IsRetiredTerminal.ShouldBeTrue();

            runtime.ViewModel.ApplyRow(runtime.ViewModel.Row with { Status = AgentRowStatus.Running, IsLost = false });

            runtime.IsRetiredTerminal.ShouldBeTrue("resuming a retired record does not revive it");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(SessionKind kind, ISteeringChannel? channel, string sessionId = "session-1", bool closed = false, bool lost = false)
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
            Kind = kind,
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
        });
    }
}
