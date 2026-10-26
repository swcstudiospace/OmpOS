using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.Parity;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>The per-session runtime a head creates once per agent: the turn logic the single-session shell used to hold, without any window.</summary>
public sealed class AgentSessionRuntimeTests
{
    private static readonly ScriptedTurn Reply = new([new TextDeltaEvent("a reply"), new CompletedEvent("end_turn")]);

    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>A sent prompt is echoed into the transcript, streams the turn, and reports Started then Completed.</summary>
    [Fact]
    public async Task ASentPromptStreamsTheTurnAndReportsStartedThenCompleted()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new ScriptedPromptChannel(Reply);
            var runtime = Create(channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var changes = new List<SessionTurnKind>();
            runtime.TurnChanged += (_, e) => changes.Add(e.Kind);

            (await runtime.Composer.SendFromAsync("do it").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);

            channel.Prompts.ShouldBe(["do it"]);
            runtime.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldBe(["do it"]);
            runtime.Session.State.ShouldBe(SessionViewState.Success);
            changes.ShouldBe([SessionTurnKind.Started, SessionTurnKind.Completed]);
            runtime.Runs.IsRunActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A fault shows under its turn, raises the banner text, drops held messages with a not-sent line, and reports Faulted with the reason.</summary>
    [Fact]
    public async Task AFaultEndsTheRunWithTheBannerTextAndAFaultedEvent()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new ScriptedPromptChannel(new ScriptedTurn([new TextDeltaEvent("partial")], Fault: "model link down"));
            var runtime = Create(channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var changes = new List<SessionTurnEventArgs>();
            runtime.TurnChanged += (_, e) => changes.Add(e);

            (await runtime.Composer.SendFromAsync("do it").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);

            runtime.ErrorText.ShouldBe("This turn failed: model link down");
            changes.Select(change => change.Kind).ShouldBe([SessionTurnKind.Started, SessionTurnKind.Faulted]);
            changes[^1].Reason.ShouldBe("model link down");
            runtime.Session.Tree.Blocks.OfType<ErrorBlock>().ShouldHaveSingleItem();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A message the run holds is sent as its own turn, in send order, once the active turn ends; stop drops it and says so.</summary>
    [Fact]
    public async Task HeldMessagesRunInSendOrderAndStopDropsThem()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new ScriptedPromptChannel(Working, Reply);
            var runtime = Create(channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);

            (await runtime.Composer.SendFromAsync("first").ConfigureAwait(true)).ShouldBeTrue();
            await runtime.Runs.SteerAsync("second", token).ConfigureAwait(true);
            runtime.Runs.SteersHeld.ShouldBe(1);
            channel.Prompts.ShouldBe(["first"], "the steer waits behind the active turn");

            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);

            channel.CancelCount.ShouldBe(1);
            channel.Prompts.ShouldBe(["first"], "a stopped run sends nothing more");
            runtime.Runs.SteersHeld.ShouldBe(0);
            runtime.Session.Tree.Blocks.OfType<MarkdownBlock>().Select(block => block.Markdown).ShouldContain("1 queued message was not sent.", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An unconnected send path refuses the send with the status line text and starts no run.</summary>
    [Fact]
    public async Task AnUnconnectedSessionRefusesTheSend()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(new UnconnectedPromptChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var feedback = 0;
            runtime.FeedbackChangedForTests(() => feedback++);

            (await runtime.Composer.SendFromAsync("hello").ConfigureAwait(true)).ShouldBeFalse();

            runtime.StatusText.ShouldBe(MainView.RefusedSendStatus);
            runtime.Composer.IsStreaming.ShouldBeFalse();
            runtime.Runs.IsRunActive.ShouldBeFalse();
            feedback.ShouldBeGreaterThan(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Stop with nothing running says so on the status line.</summary>
    [Fact]
    public async Task StopWithNothingRunningReportsIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(new ScriptedPromptChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);

            await runtime.Composer.StopAsync().ConfigureAwait(true);

            runtime.StatusText.ShouldBe(MainView.NoResponseStatus);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Observing pumps the stage source into the session's timeline and loads the context; each session sees only its own scope.</summary>
    [Fact]
    public async Task ObservingPumpsStagesAndContextForTheSessionsOwnScope()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var stages = new ScriptedStageSource();
            var context = new TaskContext { Title = "Ship it", TaskId = "T-1", SharedMemory = [], Lessons = [], Warnings = [] };
            var runtime = Create(new ScriptedPromptChannel(), stages, sessionId: "one", context);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            runtime.StartObserving();
            runtime.StartObserving();
            stages.Emit(new StageEvent
            {
                SessionId = "one",
                Source = StageSource.Ultrathink,
                StageId = "plan",
                Title = "Planned",
                Status = StageStatus.Succeeded,
                Timestamp = DateTimeOffset.UnixEpoch,
            });

            await ShellWait.UntilAsync(() => runtime.ViewModel.Timeline.HasEvents, "the stage to fold", token).ConfigureAwait(true);

            runtime.ViewModel.Timeline.StatusOf(StageSource.Ultrathink).ShouldBe(StageStatus.Succeeded);
            stages.ObservedScope.ShouldNotBeNull().SessionId.ShouldBe("one");
            await runtime.ViewModel.ActivateAsync().ConfigureAwait(true);
            await ShellWait.UntilAsync(() => runtime.ViewModel.Context.HasCard, "the context of the session to load", token).ConfigureAwait(true);
            runtime.ViewModel.Context.TaskId.ShouldBe("T-1");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A stage source that faults becomes a Degraded stage of the engine and never an unobserved exception.</summary>
    [Fact]
    public async Task AFaultInTheStagePumpBecomesADegradedEngineStage()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var stages = new ScriptedStageSource();
            var runtime = Create(new ScriptedPromptChannel(), stages);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            runtime.StartObserving();

            stages.Fault(new InvalidOperationException("the observer crashed"));

            await ShellWait.UntilAsync(() => runtime.ViewModel.Timeline.StatusOf(StageSource.Omp) == StageStatus.Degraded, "the engine stage to degrade", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Disposing stops a turn in flight, disposes the send path once, ends observation and tolerates a second call.</summary>
    [Fact]
    public async Task DisposingStopsTheTurnDisposesThePromptsOnceAndIsIdempotent()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new CountingPromptChannel(new ScriptedPromptChannel(Working));
            var runtime = Create(channel);
            runtime.StartObserving();
            (await runtime.Composer.SendFromAsync("long task").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => runtime.Session.State == SessionViewState.Streaming, "the transcript to stream", token).ConfigureAwait(true);

            await runtime.DisposeAsync().ConfigureAwait(true);
            await runtime.DisposeAsync().ConfigureAwait(true);

            channel.DisposeCount.ShouldBe(1, "the send path is disposed exactly once");
            channel.Inner.CancelCount.ShouldBe(1, "a turn in flight is stopped before the engine goes");
            Should.Throw<ObjectDisposedException>(() => runtime.StartObserving());
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The runtime owns its view model: its parts are the runtime's own and the panel finds the runtime from the view model.</summary>
    [Fact]
    public async Task TheRuntimeOwnsTheSessionViewModel()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var runtime = Create(new ScriptedPromptChannel());
            await using var runtimeScope = runtime.ConfigureAwait(true);

            runtime.ViewModel.Session.ShouldBeSameAs(runtime.Session);
            runtime.ViewModel.Composer.ShouldBeSameAs(runtime.Composer);
            runtime.ViewModel.ApprovalCoordinator.ShouldBeSameAs(runtime.RunControls.Coordinator);
            runtime.ViewModel.Id.ShouldBe("session-1");
            AgentSessionRuntime.For(runtime.ViewModel).ShouldBeSameAs(runtime, "the main view finds the runtime of the session it shows");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A permission request from the engine becomes a pending approval of this session only, mirrored on the session view model.</summary>
    [Fact]
    public async Task APermissionRequestBecomesAPendingApprovalOfThisSession()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new ScriptedPromptChannel(new ScriptedTurn([new PermissionRequestEvent("call-1", "write_file", "{}", "req-1")], HoldUntilCancelled: true));
            var runtime = Create(channel);
            await using var runtimeScope = runtime.ConfigureAwait(true);
            var other = Create(new ScriptedPromptChannel(), sessionId: "session-2");
            await using var otherScope = other.ConfigureAwait(true);

            (await runtime.Composer.SendFromAsync("write it").ConfigureAwait(true)).ShouldBeTrue();
            await ShellWait.UntilAsync(() => runtime.RunControls.Approvals.Count == 1, "the approval to appear", token).ConfigureAwait(true);

            runtime.RunControls.Approvals.Single().RequestId.ShouldBe("req-1");
            runtime.RunControls.Approvals.Single().SessionId.ShouldBe("session-1");
            runtime.ViewModel.PendingApprovals.ShouldHaveSingleItem().RequestId.ShouldBe("req-1");
            other.RunControls.Approvals.ShouldBeEmpty("another session sees nothing");

            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.UntilAsync(() => !runtime.Composer.IsStreaming, "the run to end", token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static AgentSessionRuntime Create(IPromptChannel prompts, ScriptedStageSource? stages = null, string sessionId = "session-1", TaskContext? context = null)
    {
        var now = DateTimeOffset.UnixEpoch;
        var row = AgentRowFor(sessionId, now);
        return new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = row,
            Prompts = prompts,
            Approvals = new UnconnectedToolApprovalBridge(),
            Scope = new StageObservationScope
            {
                SessionId = sessionId,
                WorkspaceRoot = Environment.CurrentDirectory,
                SessionStateDirectory = Path.Combine(Path.GetTempPath(), "ompos-shell-tests", sessionId),
            },
            Stages = stages ?? new ScriptedStageSource(),
            Context = new ScriptedTaskContext(context),
        });
    }

    private static AgentRow AgentRowFor(string sessionId, DateTimeOffset now)
    {
        var clock = new ShellClock();
        var catalog = new FakeAgentCatalog();
        catalog.Put(new SessionSummary
        {
            Id = sessionId,
            Title = $"Agent {sessionId}",
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = now,
            LastActivity = now,
        });
        using var model = new SessionCatalogModel(catalog, clock);
        model.Refresh();
        return model.Sessions.Single();
    }

    private sealed class CountingPromptChannel(ScriptedPromptChannel inner) : IPromptChannel
    {
        public ScriptedPromptChannel Inner { get; } = inner;

        public int DisposeCount { get; private set; }

        public IAsyncEnumerable<HarnessEvent> SubmitAsync(string prompt, CancellationToken cancellationToken) => Inner.SubmitAsync(prompt, cancellationToken);

        public Task CancelAsync(CancellationToken cancellationToken) => Inner.CancelAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return Inner.DisposeAsync();
        }
    }
}
