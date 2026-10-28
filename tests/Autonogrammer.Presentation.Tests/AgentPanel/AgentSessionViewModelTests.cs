using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Runs;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;
using static Autonogrammer.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// The per-session composition: steering badge, closed and lost sessions with reopen and resume, panel notices and their dismissal,
/// the run summary and its announcement, stage observation that cannot take the panel down, and the wiring between the parts.
/// </summary>
public sealed class AgentSessionViewModelTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly ScriptedRunInput _input = new();
    private readonly RunInputScheduler _scheduler;
    private readonly SessionViewModel _session = new();

    /// <summary>Creates the fixture with an idle scheduler.</summary>
    public AgentSessionViewModelTests()
    {
        _scheduler = new RunInputScheduler(_input);
    }

    /// <summary>The steering badge reads idle with no run, available during a run, and not recorded without a recorder.</summary>
    [Fact]
    public void TheSteeringBadgeFollowsTheRunAndTheRecorder()
    {
        using var withRecorder = NewViewModel(recorder: (_, _) => Task.CompletedTask);
        using var withoutRecorder = NewViewModel();
        withRecorder.SteeringBadgeText.ShouldBe("Steering idle");

        _scheduler.RunStarted();

        withRecorder.SteeringBadgeText.ShouldBe("Steering available");
        withRecorder.SteeringBadgeName.ShouldBe("Steering availability: Steering available. Messages are delivered after the active turn.");
        withoutRecorder.SteeringBadgeText.ShouldBe("Steering available, not recorded");
    }

    /// <summary>An unreachable Substrate turns the badge to not recorded and tells the steering and context models.</summary>
    [Fact]
    public void AnUnreachableSubstrateTurnsTheBadgeToNotRecorded()
    {
        using var viewModel = NewViewModel(recorder: (_, _) => Task.CompletedTask);
        _scheduler.RunStarted();
        var announced = PropertyChangeRecorder.Record(viewModel);

        viewModel.ApplyStageEvent(Stage(StageSource.Substrate, "brief", StageStatus.Unavailable, Now()));

        viewModel.SteeringBadgeText.ShouldBe("Steering available, not recorded");
        viewModel.Steering.IsSubstrateReachable.ShouldBeFalse();
        viewModel.Context.SubstrateAvailable.ShouldBe(false);
        announced.ShouldAnnounce(nameof(AgentSessionViewModel.NoticeKind));

        viewModel.ApplyStageEvent(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, Now().AddSeconds(1)));

        viewModel.SteeringBadgeText.ShouldBe("Steering available");
        viewModel.Context.SubstrateAvailable.ShouldBe(true);
    }

    /// <summary>While Substrate has not answered yet, its reachability is unknown for the context and assumed for steering.</summary>
    [Fact]
    public void ASubstrateThatHasNotAnsweredIsUnknown()
    {
        using var viewModel = NewViewModel(recorder: (_, _) => Task.CompletedTask);

        viewModel.ApplyStageEvent(Stage(StageSource.Substrate, "brief", StageStatus.Running, Now()));

        viewModel.Context.SubstrateAvailable.ShouldBeNull();
        viewModel.Steering.IsSubstrateReachable.ShouldBeTrue();
    }

    /// <summary>A closed, lost or failed session offers no steering.</summary>
    [Fact]
    public void ClosedLostAndFailedSessionsOfferNoSteering()
    {
        using var viewModel = NewViewModel(recorder: (_, _) => Task.CompletedTask);
        _scheduler.RunStarted();
        viewModel.SteeringBadgeText.ShouldBe("Steering available");

        viewModel.MarkClosed();
        viewModel.SteeringBadgeText.ShouldBe("Steering unavailable");
        viewModel.SteeringBadgeName.ShouldBe("Steering availability: Steering unavailable. Messages are delivered after the active turn.");
        viewModel.MarkReopened();
        viewModel.SteeringBadgeText.ShouldBe("Steering available");

        viewModel.ApplyRow(Row(SessionId, SessionLifecycle.Lost, _clock));
        viewModel.SteeringBadgeText.ShouldBe("Steering unavailable");

        viewModel.ApplyRow(Row(SessionId, SessionLifecycle.Failed, _clock));
        viewModel.SteeringBadgeText.ShouldBe("Steering unavailable");

        viewModel.ApplyRow(Row(SessionId, SessionLifecycle.Running, _clock));
        viewModel.SteeringBadgeText.ShouldBe("Steering available");
        viewModel.Session.ApplyEvent(StreamOf(new ErrorPayload { Message = "boom", Retryable = false }));
        viewModel.SteeringBadgeText.ShouldBe("Steering unavailable");
    }

    /// <summary>A closed session reads as closed and the reopen command restarts it.</summary>
    [Fact]
    public async Task AClosedSessionCanBeReopened()
    {
        var reopened = 0;
        var closedRow = Row(SessionId, SessionLifecycle.Paused, _clock, options: new SessionCatalogOptions { IsClosed = _ => true });
        using var viewModel = NewViewModel(row: closedRow, reopen: _ =>
        {
            reopened++;
            return Task.CompletedTask;
        });
        viewModel.IsClosed.ShouldBeTrue();
        viewModel.ReopenCommand.CanExecute(null).ShouldBeTrue();
        viewModel.CloseCommand.CanExecute(null).ShouldBeFalse();

        await viewModel.ReopenCommand.ExecuteAsync(null).ConfigureAwait(true);

        reopened.ShouldBe(1);
        viewModel.IsClosed.ShouldBeFalse();
        viewModel.ActionError.ShouldBeNull();
        viewModel.ReopenCommand.CanExecute(null).ShouldBeFalse();
        viewModel.CloseCommand.CanExecute(null).ShouldBeTrue();
    }

    /// <summary>A reopen that fails leaves the session closed and says why in plain text.</summary>
    [Fact]
    public async Task AFailedReopenLeavesTheSessionClosedAndSaysWhy()
    {
        var closedRow = Row(SessionId, SessionLifecycle.Paused, _clock, options: new SessionCatalogOptions { IsClosed = _ => true });
        using var viewModel = NewViewModel(row: closedRow, reopen: _ => throw new InvalidOperationException("omp not found\u0007"));

        await viewModel.ReopenCommand.ExecuteAsync(null).ConfigureAwait(true);

        viewModel.IsClosed.ShouldBeTrue();
        viewModel.ActionError.ShouldBe("Could not reopen this agent. omp not found.");
    }

    /// <summary>A reopen is not offered without a reopen callback or for an open session.</summary>
    [Fact]
    public void ReopenNeedsAClosedSessionAndACallback()
    {
        using var open = NewViewModel(reopen: _ => Task.CompletedTask);
        var closedRow = Row(SessionId, SessionLifecycle.Paused, _clock, options: new SessionCatalogOptions { IsClosed = _ => true });
        using var noCallback = NewViewModel(row: closedRow);

        open.ReopenCommand.CanExecute(null).ShouldBeFalse();
        noCallback.ReopenCommand.CanExecute(null).ShouldBeFalse();
    }

    /// <summary>A lost session shows the lost notice, resumes through the callback, then shows the resumed notice until dismissed.</summary>
    [Fact]
    public async Task ALostSessionCanBeResumed()
    {
        var resumed = 0;
        using var viewModel = NewViewModel(
            row: Row(SessionId, SessionLifecycle.Lost, _clock),
            resume: _ =>
            {
                resumed++;
                return Task.CompletedTask;
            });
        viewModel.IsLost.ShouldBeTrue();
        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.Lost);
        viewModel.NoticeText.ShouldBe("This agent was running when Autonogrammer closed. Resume it to continue from its last saved point.");
        viewModel.DismissNoticeCommand.CanExecute(null).ShouldBeFalse();
        viewModel.ResumeCommand.CanExecute(null).ShouldBeTrue();

        await viewModel.ResumeCommand.ExecuteAsync(null).ConfigureAwait(true);

        resumed.ShouldBe(1);
        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.Resumed);
        viewModel.NoticeText.ShouldBe("Resumed. The agent continues from its last saved point.");
        viewModel.Timeline.StatusAnnouncement.ShouldBe("Resumed.");
        viewModel.DismissNoticeCommand.CanExecute(null).ShouldBeTrue();

        viewModel.ApplyRow(Row(SessionId, SessionLifecycle.Running, _clock));
        viewModel.IsLost.ShouldBeFalse();
        viewModel.ResumeCommand.CanExecute(null).ShouldBeFalse();
        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.Resumed);

        await viewModel.DismissNoticeCommand.ExecuteAsync(null).ConfigureAwait(true);

        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.None);
        viewModel.HasNotice.ShouldBeFalse();
        viewModel.NoticeText.ShouldBeNull();
    }

    /// <summary>A resume that fails keeps the lost notice and says why.</summary>
    [Fact]
    public async Task AFailedResumeKeepsTheLostNoticeAndSaysWhy()
    {
        using var viewModel = NewViewModel(row: Row(SessionId, SessionLifecycle.Lost, _clock), resume: _ => throw new IOException("state file is locked"));

        await viewModel.ResumeCommand.ExecuteAsync(null).ConfigureAwait(true);

        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.Lost);
        viewModel.ActionError.ShouldBe("Could not resume this agent. state file is locked.");
    }

    /// <summary>Degraded stages raise a counted notice that the user can dismiss; a new degraded stage raises it again.</summary>
    [Fact]
    public async Task ADegradedNoticeCountsStagesAndCanBeDismissed()
    {
        using var viewModel = NewViewModel();
        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.None);

        viewModel.ApplyStageEvent(Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, Now()));

        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.Degraded);
        viewModel.NoticeText.ShouldBe("1 pipeline stage is degraded. The agent keeps running.");

        await viewModel.DismissNoticeCommand.ExecuteAsync(null).ConfigureAwait(true);
        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.None);

        viewModel.ApplyStageEvent(Stage(StageSource.Swarm, "subagents", StageStatus.Unavailable, Now()));

        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.Degraded);
        viewModel.NoticeText.ShouldBe("2 pipeline stages are degraded. The agent keeps running.");
    }

    /// <summary>When all four plugins are unavailable the notice says the agent runs as plain omp, and a dismissal holds until they recover.</summary>
    [Fact]
    public async Task NoPluginsRunsAsPlainOmp()
    {
        using var viewModel = NewViewModel();
        foreach (var source in new[] { StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd })
        {
            viewModel.ApplyStageEvent(Stage(source, "availability", StageStatus.Unavailable, Now()));
        }

        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.NoPlugins);
        viewModel.NoticeText.ShouldBe("No plugins are available. This agent runs as plain omp.");

        await viewModel.DismissNoticeCommand.ExecuteAsync(null).ConfigureAwait(true);
        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.None);

        viewModel.ApplyStageEvent(Stage(StageSource.Gsd, "availability", StageStatus.Succeeded, Now().AddSeconds(1)));
        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.Degraded);
        viewModel.ApplyStageEvent(Stage(StageSource.Gsd, "availability", StageStatus.Unavailable, Now().AddSeconds(2)));

        viewModel.NoticeKind.ShouldBe(PanelNoticeKind.NoPlugins);
    }

    /// <summary>The run summary exists only in the success state and counts recorded steering messages.</summary>
    [Fact]
    public async Task TheRunSummaryExistsOnlyAfterSuccessAndCountsRecordedSteering()
    {
        using var viewModel = NewViewModel(recorder: (_, _) => Task.CompletedTask);
        viewModel.ApplyStageEvent(Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, Now()));
        viewModel.ApplyStageEvent(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, Now()));
        viewModel.RunSummaryText.ShouldBeNull();
        viewModel.Session.ApplyEvent(StreamOf(new TextDeltaPayload { Delta = "working" }));
        _scheduler.RunStarted();
        await viewModel.Steering.SubmitAsync("use the smaller model", SteeringMode.Steer, TestContext.Current.CancellationToken).ConfigureAwait(true);

        viewModel.Session.ApplyEvent(StreamOf(new CompletedPayload { Reason = "end_turn" }));

        viewModel.RunSummaryText.ShouldBe("Finished. Ultrathink: planned. Substrate: ok, 1 steering message recorded.");
        viewModel.Timeline.StatusAnnouncement.ShouldBe("Agent finished. Ultrathink: planned. Substrate: ok, 1 steering message recorded.");
    }

    /// <summary>A transcript that loads straight to success is not a finished run and announces nothing.</summary>
    [Fact]
    public void ALoadedTranscriptDoesNotAnnounceFinished()
    {
        using var viewModel = NewViewModel();

        viewModel.Session.ApplyEvent(StreamOf(new CompletedPayload { Reason = "end_turn" }));

        viewModel.Timeline.StatusAnnouncement.ShouldBeNull();
        viewModel.RunSummaryText.ShouldBe("Finished.");
    }

    /// <summary>Close asks the owner to close, and closing needs a confirmation while a run is active.</summary>
    [Fact]
    public async Task CloseAsksTheOwnerAndNeedsConfirmationWhileRunning()
    {
        using var viewModel = NewViewModel(row: Row(SessionId, SessionLifecycle.Running, _clock, title: "Fix the build"));
        var requested = 0;
        viewModel.CloseRequested += (_, _) => requested++;
        viewModel.RequiresCloseConfirmation.ShouldBeFalse();

        _scheduler.RunStarted();
        viewModel.RequiresCloseConfirmation.ShouldBeTrue();
        viewModel.CloseConfirmationText.ShouldBe("Close \"Fix the build\"? The agent is running. Closing stops its engine and ends the run. Partial output is kept.");

        await viewModel.CloseCommand.ExecuteAsync(null).ConfigureAwait(true);

        requested.ShouldBe(1);
    }

    /// <summary>A streaming transcript also needs the confirmation.</summary>
    [Fact]
    public void AStreamingTranscriptNeedsTheCloseConfirmation()
    {
        using var viewModel = NewViewModel();

        viewModel.Session.ApplyEvent(StreamOf(new TextDeltaPayload { Delta = "x" }));

        viewModel.RequiresCloseConfirmation.ShouldBeTrue();
    }

    /// <summary>A row of another session is ignored, an identical row changes nothing, and a new row updates title and status.</summary>
    [Fact]
    public void ApplyRowTakesOnlyItsOwnSessionsRows()
    {
        var first = Row(SessionId, SessionLifecycle.Running, _clock, title: "First title");
        using var viewModel = NewViewModel(row: first);
        var announced = PropertyChangeRecorder.Record(viewModel);

        viewModel.ApplyRow(Row("other", SessionLifecycle.Failed, _clock));
        viewModel.ApplyRow(first);
        announced.ShouldBeEmpty();

        viewModel.ApplyRow(Row(SessionId, SessionLifecycle.Completed, _clock, title: "Second title"));

        viewModel.Title.ShouldBe("Second title");
        viewModel.Row.Status.ShouldBe(AgentRowStatus.Completed);
        announced.ShouldAnnounce(nameof(AgentSessionViewModel.Title));
        announced.ShouldAnnounce(nameof(AgentSessionViewModel.Row));
    }

    /// <summary>Observing a source folds its events into the timeline.</summary>
    [Fact]
    public async Task ObservingASourceFoldsItsEvents()
    {
        using var viewModel = NewViewModel();
        var source = new ScriptedStageSource(
            [
                Stage(StageSource.Ultrathink, "plan", StageStatus.Running, Now()),
                Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, Now().AddSeconds(1), detail: "8 nodes"),
            ],
            fault: null);

        await viewModel.ObserveAsync(source, TestContext.Current.CancellationToken).ConfigureAwait(true);

        viewModel.Timeline.Blocks.Single(block => block.Source == StageSource.Ultrathink).Rows.Single().Status.ShouldBe(StageStatus.Succeeded);
        source.Scopes.Single().SessionId.ShouldBe(SessionId);
    }

    /// <summary>A source that faults mid-way becomes a degraded engine stage with fixed text, and the fault text never reaches the panel.</summary>
    [Fact]
    public async Task AFaultingSourceBecomesADegradedStageWithFixedText()
    {
        using var viewModel = NewViewModel();
        var source = new ScriptedStageSource(
            [Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now())],
            new InvalidOperationException("token=sk-secret"));

        await viewModel.ObserveAsync(source, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var engine = viewModel.Timeline.Blocks.Single(block => block.Source == StageSource.Omp).Rows.Single();
        engine.Status.ShouldBe(StageStatus.Degraded);
        engine.Detail.ShouldBe("Stage updates stopped. The agent keeps running.");
        viewModel.Timeline.Blocks.Single(block => block.Source == StageSource.Swarm).Rows.Single().Status.ShouldBe(StageStatus.Running);
    }

    /// <summary>Cancelling the observation ends it quietly.</summary>
    [Fact]
    public async Task CancellingTheObservationEndsItQuietly()
    {
        using var viewModel = NewViewModel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync().ConfigureAwait(true);
        var source = new ScriptedStageSource([Stage(StageSource.Swarm, "subagents", StageStatus.Running, Now())], fault: null);

        await viewModel.ObserveAsync(source, cancellation.Token).ConfigureAwait(true);

        viewModel.Timeline.Blocks.Single(block => block.Source == StageSource.Omp).Rows.ShouldBeEmpty();
    }

    /// <summary>Activating loads the task context with the session's scope; the phase flows into the pipeline and the task title shows.</summary>
    [Fact]
    public async Task ActivatingLoadsTheContextAndFeedsThePhaseToThePipeline()
    {
        var phase = new GsdPhaseInfo { Number = "46", Name = "Agent Panel", Status = "in progress", PlansComplete = 2, PlansTotal = 5 };
        var source = ScriptedContextSource.Returning(Context(phase: phase));
        using var viewModel = NewViewModel(source: source);
        viewModel.ApplyStageEvent(Stage(StageSource.Gsd, "phase", StageStatus.Succeeded, Now()));
        viewModel.TaskTitle.ShouldBeNull();

        await viewModel.ActivateAsync().ConfigureAwait(true);

        source.Requests.Single().Scope.SessionId.ShouldBe(SessionId);
        viewModel.TaskTitle.ShouldBe("Ship the panel");
        viewModel.GraphId.ShouldBe("ut-mvp0bh59-2dd6ef4c");
        viewModel.Timeline.Phase.ShouldBe(phase);
        viewModel.Timeline.Pills.Single(pill => pill.Source == StageSource.Gsd).Text.ShouldBe("GSD 46 \u00B7 2 of 5");
    }

    /// <summary>Deactivating cancels the running context load.</summary>
    [Fact]
    public void DeactivatingCancelsTheContextLoad()
    {
        var source = new ScriptedContextSource((_, token) => new TaskCompletionSource<TaskContext>().Task.WaitAsync(token));
        using var viewModel = NewViewModel(source: source);
        _ = viewModel.ActivateAsync();

        viewModel.Deactivate();

        source.Requests.Single().Token.IsCancellationRequested.ShouldBeTrue();
        viewModel.Context.State.ShouldBe(TaskContextState.Unavailable);
    }

    /// <summary>After disposal the parts no longer drive the view model.</summary>
    [Fact]
    public void DisposeStopsFollowingTheParts()
    {
        var viewModel = NewViewModel();
        var announced = PropertyChangeRecorder.Record(viewModel);

        viewModel.Dispose();
        viewModel.Timeline.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, Now()));
        _scheduler.RunStarted();

        announced.ShouldBeEmpty();
    }

    private DateTimeOffset Now() => _clock.GetUtcNow();

    private AgentSessionViewModel NewViewModel(
        AgentRow? row = null,
        ITaskContextSource? source = null,
        Func<SteeringEntry, CancellationToken, Task>? recorder = null,
        Func<CancellationToken, Task>? resume = null,
        Func<CancellationToken, Task>? reopen = null)
    {
        var composer = new ComposerViewModel(_ => Task.FromResult(true), () => Task.CompletedTask);
        return new AgentSessionViewModel(new AgentSessionOptions
        {
            Row = row ?? Row(SessionId, SessionLifecycle.Running, _clock),
            Session = _session,
            Composer = composer,
            Scheduler = _scheduler,
            Scope = Scope(),
            TimeProvider = _clock,
            TaskContextSource = source,
            SteeringRecorder = recorder,
            Resume = resume,
            Reopen = reopen,
        });
    }

    private sealed class ScriptedStageSource : IStageSource
    {
        private readonly IReadOnlyList<StageEvent> _events;
        private readonly Exception? _fault;

        internal ScriptedStageSource(IReadOnlyList<StageEvent> events, Exception? fault)
        {
            _events = events;
            _fault = fault;
        }

        internal List<StageObservationScope> Scopes { get; } = [];

        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Scopes.Add(scope);
            foreach (var stageEvent in _events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return stageEvent;
            }

            await Task.Yield();
            if (_fault is not null)
            {
                throw _fault;
            }
        }
    }
}
