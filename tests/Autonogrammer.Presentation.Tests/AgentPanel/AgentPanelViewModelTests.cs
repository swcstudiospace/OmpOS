using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// The panel root: Empty, Loading, Ready and Error states with their banner and retry, selection rebinding with cancellation of the
/// outstanding context load, New agent, closing with next-row selection, and the single publication of property changes.
/// </summary>
public sealed class AgentPanelViewModelTests : IDisposable
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();
    private readonly ScriptedPanelHost _host;
    private SessionCatalogModel? _model;
    private AgentPanelViewModel? _panel;

    /// <summary>Creates the fixture with an empty registry.</summary>
    public AgentPanelViewModelTests()
    {
        _host = new ScriptedPanelHost(_catalog, _clock);
    }

    /// <summary>Releases the panel and the catalog model the test created.</summary>
    public void Dispose()
    {
        _panel?.Dispose();
        _model?.Dispose();
    }

    /// <summary>With no session the panel is Empty with no selection and no banner.</summary>
    [Fact]
    public void WithNoSessionThePanelIsEmpty()
    {
        var panel = NewPanel();

        panel.State.ShouldBe(PanelState.Empty);
        panel.SessionCount.ShouldBe(0);
        panel.Selected.ShouldBeNull();
        panel.BannerText.ShouldBeNull();
        panel.RetryLoadingCommand.CanExecute(null).ShouldBeFalse();
    }

    /// <summary>With sessions the panel is Ready, and a new session in the registry moves it from Empty to Ready.</summary>
    [Fact]
    public void WithSessionsThePanelIsReady()
    {
        var panel = NewPanel();
        var announced = PropertyChangeRecorder.Record(panel);

        AddSessions("a", "b");

        panel.State.ShouldBe(PanelState.Ready);
        panel.SessionCount.ShouldBe(2);
        announced.ShouldAnnounce(nameof(AgentPanelViewModel.State));
        announced.ShouldAnnounce(nameof(AgentPanelViewModel.SessionCount));
    }

    /// <summary>The panel is Loading while any initialisation scope is open, and the panel stays usable.</summary>
    [Fact]
    public void ThePanelIsLoadingWhileInitialisationIsOutstanding()
    {
        AddSessions("a");
        var panel = NewPanel();
        panel.State.ShouldBe(PanelState.Ready);

        var outer = panel.BeginInitialising();
        var inner = panel.BeginInitialising();
        panel.State.ShouldBe(PanelState.Loading);

        inner.Dispose();
        inner.Dispose();
        panel.State.ShouldBe(PanelState.Loading);

        outer.Dispose();
        panel.State.ShouldBe(PanelState.Ready);
    }

    /// <summary>Loading ends in Empty when the load found no session.</summary>
    [Fact]
    public void LoadingEndsInEmptyWhenNothingWasFound()
    {
        var panel = NewPanel();

        using (panel.BeginInitialising())
        {
            panel.State.ShouldBe(PanelState.Loading);
            panel.SelectSession("missing").ShouldBeFalse();
            panel.Selected.ShouldBeNull();
        }

        panel.State.ShouldBe(PanelState.Empty);
    }

    /// <summary>A reported failure shows the Error state with the banner text, and retry is offered only then.</summary>
    [Fact]
    public void AReportedFailureShowsTheBannerAndOffersRetry()
    {
        AddSessions("a");
        var panel = NewPanel();

        panel.ReportLoadFailure("The agent state could not be read.");

        panel.State.ShouldBe(PanelState.Error);
        panel.BannerText.ShouldBe("Could not load your agents. The agent state could not be read. Your saved agents are not lost.");
        panel.RetryLoadingCommand.CanExecute(null).ShouldBeTrue();
        panel.SessionCount.ShouldBe(1);
    }

    /// <summary>A hostile reason is made plain and capped before it reaches the banner.</summary>
    [Fact]
    public void TheBannerReasonIsPlain()
    {
        var panel = NewPanel();

        panel.ReportLoadFailure("\u202Edisk\u0007 full");

        panel.BannerText.ShouldBe("Could not load your agents. disk full. Your saved agents are not lost.");
    }

    /// <summary>Retry shows Loading while the reload runs, then Ready, and clears the banner.</summary>
    [Fact]
    public async Task RetryShowsLoadingThenReady()
    {
        AddSessions("a");
        var panel = NewPanel();
        panel.ReportLoadFailure("offline");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _host.ReloadGate = gate.Task;

        var retry = panel.RetryLoadingAsync(TestContext.Current.CancellationToken);

        panel.State.ShouldBe(PanelState.Loading);
        panel.BannerText.ShouldBeNull();
        gate.SetResult();
        await retry.ConfigureAwait(true);

        panel.State.ShouldBe(PanelState.Ready);
        panel.BannerText.ShouldBeNull();
        _host.ReloadCount.ShouldBe(1);
    }

    /// <summary>The retry command runs the same reload.</summary>
    [Fact]
    public async Task TheRetryCommandReloads()
    {
        var panel = NewPanel();
        panel.ReportLoadFailure("offline");

        await panel.RetryLoadingCommand.ExecuteAsync(null).ConfigureAwait(true);

        panel.State.ShouldBe(PanelState.Empty);
        _host.ReloadCount.ShouldBe(1);
    }

    /// <summary>A retry that fails again shows the Error state with the new reason, and does not throw.</summary>
    [Fact]
    public async Task AFailingRetryShowsTheNewReason()
    {
        var panel = NewPanel();
        panel.ReportLoadFailure("offline");
        _host.ReloadFailure = new InvalidOperationException("plugins folder missing");

        await panel.RetryLoadingAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        panel.State.ShouldBe(PanelState.Error);
        panel.BannerText.ShouldBe("Could not load your agents. plugins folder missing. Your saved agents are not lost.");
    }

    /// <summary>A catalog that cannot be read puts the panel in Error with its reason; a good read and retry recover it.</summary>
    [Fact]
    public async Task AnUnreadableCatalogPutsThePanelInError()
    {
        AddSessions("a");
        var panel = NewPanel();

        _catalog.Failure = new IOException("sessions.json is locked");
        _catalog.RaiseChanged();

        panel.State.ShouldBe(PanelState.Error);
        panel.BannerText.ShouldBe("Could not load your agents. sessions.json is locked. Your saved agents are not lost.");
        panel.SessionCount.ShouldBe(1);

        _catalog.Failure = null;
        await panel.RetryLoadingAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        panel.State.ShouldBe(PanelState.Ready);
    }

    /// <summary>Selecting a session rebinds the selected view model, creating each view model once.</summary>
    [Fact]
    public async Task SelectingASessionRebindsTheSelectedViewModel()
    {
        AddSessions("a", "b");
        var panel = NewPanel();

        panel.SelectSession("a").ShouldBeTrue();
        var first = panel.Selected;
        panel.SelectionLoad.IsCompletedSuccessfully.ShouldBeTrue();
        first!.Id.ShouldBe("a");
        panel.SelectSession("b").ShouldBeTrue();
        panel.Selected!.Id.ShouldBe("b");
        panel.SelectSession("a").ShouldBeTrue();
        await panel.SelectionLoad.ConfigureAwait(true);

        panel.Selected.ShouldBeSameAs(first);
        _host.Opened.ShouldBe(["a", "b"]);
        panel.Catalog.SelectedId.ShouldBe("a");
    }

    /// <summary>Selecting an unknown session is refused and keeps the selection; null clears it.</summary>
    [Fact]
    public void SelectingAnUnknownSessionIsRefused()
    {
        AddSessions("a");
        var panel = NewPanel();
        panel.SelectSession("a");

        panel.SelectSession("missing").ShouldBeFalse();
        panel.Selected!.Id.ShouldBe("a");

        panel.SelectSession(null).ShouldBeTrue();
        panel.Selected.ShouldBeNull();
    }

    /// <summary>Selecting another session cancels the context load that is still outstanding for the previous one and starts the new load.</summary>
    [Fact]
    public async Task SelectingCancelsTheOutstandingContextLoad()
    {
        var gates = new Dictionary<string, TaskCompletionSource<TaskContext>>(StringComparer.Ordinal);
        var sources = new Dictionary<string, ScriptedContextSource>(StringComparer.Ordinal);
        _host.ContextSources = id =>
        {
            var gate = new TaskCompletionSource<TaskContext>(TaskCreationOptions.RunContinuationsAsynchronously);
            gates[id] = gate;
            var source = new ScriptedContextSource((_, token) => gate.Task.WaitAsync(token));
            sources[id] = source;
            return source;
        };
        AddSessions("a", "b");
        var panel = NewPanel();

        panel.SelectSession("a");
        var loadA = panel.SelectionLoad;
        sources["a"].Requests.Single().Token.IsCancellationRequested.ShouldBeFalse();
        panel.Selected!.Context.State.ShouldBe(TaskContextState.Loading);

        panel.SelectSession("b");
        var loadB = panel.SelectionLoad;

        sources["a"].Requests.Single().Token.IsCancellationRequested.ShouldBeTrue();
        sources["b"].Requests.Single().Token.IsCancellationRequested.ShouldBeFalse();
        panel.Selected.Id.ShouldBe("b");

        gates["a"].SetResult(Context(title: "late answer for a"));
        await loadA.ConfigureAwait(true);
        panel.Selected.Context.State.ShouldBe(TaskContextState.Loading);

        gates["b"].SetResult(Context(title: "answer for b"));
        await loadB.ConfigureAwait(true);
        panel.Selected.Context.Title.ShouldBe("answer for b");
        _host.ViewModels["a"].Context.State.ShouldNotBe(TaskContextState.Ready);
    }

    /// <summary>A selection change announces nothing, and only the selected session's stage transitions reach the announcement.</summary>
    [Fact]
    public void OnlyTheSelectedSessionsTransitionsAreAnnounced()
    {
        AddSessions("a", "b");
        var panel = NewPanel();
        panel.SelectSession("a");
        panel.PipelineAnnouncement.ShouldBeNull();
        var announced = PropertyChangeRecorder.Record(panel);

        _host.ViewModels["a"].ApplyStageEvent(Stage(StageSource.Swarm, "subagents", StageStatus.Running, _clock.GetUtcNow(), sessionId: "a"));

        panel.PipelineAnnouncement.ShouldBe("Swarm started.");
        announced.ShouldAnnounce(nameof(AgentPanelViewModel.PipelineAnnouncement));
        announced.ShouldAnnounce(nameof(AgentPanelViewModel.PipelineAnnouncementVersion));

        panel.SelectSession("b");
        panel.PipelineAnnouncement.ShouldBeNull();
        _host.ViewModels["b"].Timeline.Announce("quiet");
        panel.PipelineAnnouncement.ShouldBe("quiet");
        _host.ViewModels["a"].ApplyStageEvent(Stage(StageSource.Swarm, "subagents", StageStatus.Succeeded, _clock.GetUtcNow(), sessionId: "a"));
        panel.PipelineAnnouncement.ShouldBe("quiet");
    }

    /// <summary>A catalog row update reaches the open session's view model.</summary>
    [Fact]
    public void ARowUpdateReachesTheOpenViewModel()
    {
        AddSessions("a");
        var panel = NewPanel();
        panel.SelectSession("a");
        panel.Selected!.IsLost.ShouldBeFalse();

        var summary = _catalog.Sessions.Single();
        _catalog.Put(summary with { Lifecycle = SessionLifecycle.Lost });

        panel.Selected.IsLost.ShouldBeTrue();
        panel.Selected.NoticeKind.ShouldBe(PanelNoticeKind.Lost);
    }

    /// <summary>A selected session that leaves the registry clears the selection, and a session of the same id opens a fresh view model.</summary>
    [Fact]
    public void ARemovedSessionClearsTheSelectionAndItsViewModel()
    {
        AddSessions("a", "b");
        var panel = NewPanel();
        panel.SelectSession("a");
        var first = panel.Selected;

        _catalog.Remove("a");

        panel.Selected.ShouldBeNull();
        panel.SessionCount.ShouldBe(1);

        AddSessions("a");
        panel.SelectSession("a");

        panel.Selected.ShouldNotBeSameAs(first);
        _host.Opened.ShouldBe(["a", "a"]);
    }

    /// <summary>Closing the selected session closes it through the host, keeps it in the catalog as Closed, selects the next row and announces it.</summary>
    [Fact]
    public async Task ClosingTheSelectedSessionSelectsTheNextRow()
    {
        AddSessions("a", "b", "c");
        var panel = NewPanel();
        panel.SelectSession("b");

        await panel.CloseSessionAsync("b", TestContext.Current.CancellationToken).ConfigureAwait(true);

        _host.Closed.ShouldBe(["b"]);
        _host.ViewModels["b"].IsClosed.ShouldBeTrue();
        panel.Selected!.Id.ShouldBe("c");
        panel.SessionCount.ShouldBe(3);
        panel.Catalog.Sessions.Single(row => string.Equals(row.Id, "b", StringComparison.Ordinal)).StatusText.ShouldBe("Closed");
        panel.PipelineAnnouncement.ShouldBe("Agent \"Agent b\" closed.");
    }

    /// <summary>Closing the last row selects the previous one.</summary>
    [Fact]
    public async Task ClosingTheLastRowSelectsThePreviousOne()
    {
        AddSessions("a", "b");
        var panel = NewPanel();
        panel.SelectSession("b");

        await panel.CloseSessionAsync("b", TestContext.Current.CancellationToken).ConfigureAwait(true);

        panel.Selected!.Id.ShouldBe("a");
    }

    /// <summary>Closing the only session leaves it selected and closed with the panel Ready, so the user can reopen it.</summary>
    [Fact]
    public async Task ClosingTheOnlySessionLeavesItSelectedAndClosed()
    {
        AddSessions("a");
        var panel = NewPanel();
        panel.SelectSession("a");

        await panel.CloseSessionAsync("a", TestContext.Current.CancellationToken).ConfigureAwait(true);

        panel.State.ShouldBe(PanelState.Ready);
        panel.Selected!.Id.ShouldBe("a");
        panel.Selected.IsClosed.ShouldBeTrue();
        panel.Selected.SteeringBadgeText.ShouldBe("Steering unavailable");
    }

    /// <summary>Closing a session that is not selected leaves the selection where it is.</summary>
    [Fact]
    public async Task ClosingAnotherSessionKeepsTheSelection()
    {
        AddSessions("a", "b");
        var panel = NewPanel();
        panel.SelectSession("a");

        await panel.CloseSessionAsync("b", TestContext.Current.CancellationToken).ConfigureAwait(true);

        panel.Selected!.Id.ShouldBe("a");
        _host.Closed.ShouldBe(["b"]);
    }

    /// <summary>Closing an unknown session does nothing, and a blank id is an error.</summary>
    [Fact]
    public async Task ClosingAnUnknownSessionDoesNothing()
    {
        var panel = NewPanel();

        await panel.CloseSessionAsync("missing", TestContext.Current.CancellationToken).ConfigureAwait(true);

        _host.Closed.ShouldBeEmpty();
        await Should.ThrowAsync<ArgumentException>(() => panel.CloseSessionAsync(" ", TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <summary>A close requested from the session's own command or from the rail goes through the host; a failure shows as an action error and changes nothing.</summary>
    [Fact]
    public async Task CloseRequestsFromTheViewsGoThroughTheHost()
    {
        AddSessions("a", "b");
        var panel = NewPanel();
        panel.SelectSession("a");

        await panel.Selected!.CloseCommand.ExecuteAsync(null).ConfigureAwait(true);
        _host.Closed.ShouldBe(["a"]);

        panel.SelectSession("b");
        await panel.Catalog.CloseSelectedCommand.ExecuteAsync(null).ConfigureAwait(true);
        _host.Closed.ShouldBe(["a", "b"]);
        panel.ActionError.ShouldBeNull();
    }

    /// <summary>A host that cannot stop an engine leaves the session as it was and says so.</summary>
    [Fact]
    public async Task AFailedCloseIsReportedAndChangesNothing()
    {
        AddSessions("a", "b");
        var panel = NewPanel();
        panel.SelectSession("a");
        _host.CloseFailure = new InvalidOperationException("engine did not stop");

        await panel.Selected!.CloseCommand.ExecuteAsync(null).ConfigureAwait(true);

        panel.ActionError.ShouldBe("Could not close this agent. engine did not stop.");
        panel.Selected.Id.ShouldBe("a");
        _host.ViewModels["a"].IsClosed.ShouldBeFalse();
    }

    /// <summary>New agent creates a session through the host, selects it, and clears an earlier action error.</summary>
    [Fact]
    public async Task NewAgentCreatesAndSelectsASession()
    {
        var panel = NewPanel();
        panel.State.ShouldBe(PanelState.Empty);

        await panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

        _host.Created.ShouldBe(["new-1"]);
        panel.State.ShouldBe(PanelState.Ready);
        panel.SessionCount.ShouldBe(1);
        panel.Selected!.Id.ShouldBe("new-1");
        panel.Catalog.SelectedId.ShouldBe("new-1");
    }

    /// <summary>A host that cannot create a session leaves the panel as it was and says why, then recovers.</summary>
    [Fact]
    public async Task AFailedNewAgentIsReportedThenRecovers()
    {
        var panel = NewPanel();
        _host.CreateFailure = new IOException("no disk space");

        await panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

        panel.ActionError.ShouldBe("Could not start a new agent. no disk space.");
        panel.State.ShouldBe(PanelState.Empty);
        panel.Selected.ShouldBeNull();

        _host.CreateFailure = null;
        await panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

        panel.ActionError.ShouldBeNull();
        panel.Selected!.Id.ShouldBe("new-1");
    }

    /// <summary>Each property is announced once when it changes and never when an operation leaves it as it was.</summary>
    [Fact]
    public void PropertiesAreAnnouncedOnlyWhenTheyChange()
    {
        AddSessions("a");
        var panel = NewPanel();
        var announced = PropertyChangeRecorder.Record(panel);

        panel.SelectSession("a");

        announced.Count(name => string.Equals(name, nameof(AgentPanelViewModel.Selected), StringComparison.Ordinal)).ShouldBe(1);
        announced.ShouldNotContain(nameof(AgentPanelViewModel.State), StringComparer.Ordinal);
        announced.Clear();

        panel.SelectSession("a");
        panel.ReportLoadFailure("x");
        panel.ReportLoadFailure("x");

        announced.Count(name => string.Equals(name, nameof(AgentPanelViewModel.State), StringComparison.Ordinal)).ShouldBe(1);
        announced.Count(name => string.Equals(name, nameof(AgentPanelViewModel.BannerText), StringComparison.Ordinal)).ShouldBe(1);
        announced.ShouldNotContain(nameof(AgentPanelViewModel.Selected), StringComparer.Ordinal);
    }

    /// <summary>After disposal the panel ignores the catalog.</summary>
    [Fact]
    public void DisposeStopsFollowingTheCatalog()
    {
        var panel = NewPanel();
        AddSessions("a");
        panel.SelectSession("a");
        panel.Dispose();

        AddSessions("b");

        panel.SessionCount.ShouldBe(1);
    }

    /// <summary>Startup stays asynchronous, and SelectionLoad includes the context load after the model is bound.</summary>
    [Fact]
    public async Task SelectionLoadAwaitsStartupAndContext()
    {
        AddSessions("a");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contextStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new TaskCompletionSource<TaskContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new ScriptedContextSource((_, token) =>
        {
            contextStarted.SetResult();
            return context.Task.WaitAsync(token);
        });
        _host.OpenGate = (_, token) => opened.Task.WaitAsync(token);
        _host.ContextSources = _ => source;
        var panel = NewPanel();

        panel.SelectSession("a").ShouldBeTrue();
        var load = panel.SelectionLoad;
        panel.Catalog.SelectedId.ShouldBe("a");
        panel.Selected.ShouldBeNull();
        load.IsCompleted.ShouldBeFalse();
        source.Requests.ShouldBeEmpty();

        opened.SetResult();
        await contextStarted.Task.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        panel.Selected!.Id.ShouldBe("a");
        panel.Selected.Context.State.ShouldBe(TaskContextState.Loading);
        load.IsCompleted.ShouldBeFalse();

        context.SetResult(Context(title: "loaded after startup"));
        await load.ConfigureAwait(true);
        panel.Selected.Context.Title.ShouldBe("loaded after startup");
        load.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>A late successful open cannot replace the latest selection; its live model is cached without being activated.</summary>
    [Fact]
    public async Task ALateOpenDoesNotReplaceTheLatestSelection()
    {
        AddSessions("a", "b");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceA = ScriptedContextSource.Returning(Context(title: "a context"));
        _host.OpenGate = (id, _) => string.Equals(id, "a", StringComparison.Ordinal) ? opened.Task : Task.CompletedTask;
        _host.ContextSources = id => string.Equals(id, "a", StringComparison.Ordinal) ? sourceA : null;
        var panel = NewPanel();
        panel.SelectSession("a");
        var loadA = panel.SelectionLoad;

        panel.SelectSession("b");
        var loadB = panel.SelectionLoad;
        panel.Selected!.Id.ShouldBe("b");
        _host.OpenRequests[0].Token.IsCancellationRequested.ShouldBeTrue();
        loadB.IsCompleted.ShouldBeFalse();

        opened.SetResult();
        await loadB.ConfigureAwait(true);
        await loadA.ConfigureAwait(true);
        panel.Selected.Id.ShouldBe("b");
        sourceA.Requests.ShouldBeEmpty();
        panel.BannerText.ShouldBeNull();

        var cached = _host.ViewModels["a"];
        panel.SelectSession("a");
        await panel.SelectionLoad.ConfigureAwait(true);
        panel.Selected.ShouldBeSameAs(cached);
        sourceA.Requests.Count.ShouldBe(1);
        _host.Opened.ShouldBe(["a", "b"]);
    }

    /// <summary>Rapid A-B-A waits for the first A attempt to settle, reusing only a successful model and retrying cancelled or failed startup.</summary>
    /// <param name="outcome">Zero succeeds late, one cancels late, and two fails late after cleanup.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReturningToAPendingSessionDoesNotReuseAFailedAttempt(int outcome)
    {
        AddSessions("a", "b");
        var firstOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        _host.OpenGate = (id, _) =>
            string.Equals(id, "a", StringComparison.Ordinal) && ++attempts == 1 ? firstOpen.Task : Task.CompletedTask;
        var source = ScriptedContextSource.Returning(Context(title: "latest a"));
        _host.ContextSources = id => string.Equals(id, "a", StringComparison.Ordinal) ? source : null;
        var panel = NewPanel();
        panel.SelectSession("a");
        var firstLoad = panel.SelectionLoad;
        panel.SelectSession("b");
        panel.Selected!.Id.ShouldBe("b");

        panel.SelectSession("a").ShouldBeTrue();
        var latestLoad = panel.SelectionLoad;
        panel.Selected.ShouldBeNull();
        _host.Opened.ShouldBe(["a", "b"]);
        _host.OpenRequests[0].Token.IsCancellationRequested.ShouldBeTrue();
        latestLoad.IsCompleted.ShouldBeFalse();
        if (outcome == 0)
        {
            firstOpen.SetResult();
        }
        else if (outcome == 1)
        {
            firstOpen.SetCanceled(TestContext.Current.CancellationToken);
        }
        else
        {
            firstOpen.SetException(new IOException("obsolete startup failed after cleanup"));
        }

        await latestLoad.ConfigureAwait(true);
        await firstLoad.ConfigureAwait(true);
        panel.Selected!.Id.ShouldBe("a");
        panel.Selected.Context.Title.ShouldBe("latest a");
        source.Requests.Count.ShouldBe(1);
        panel.BannerText.ShouldBeNull();
        latestLoad.IsCompletedSuccessfully.ShouldBeTrue();
        if (outcome == 0)
        {
            _host.Opened.ShouldBe(["a", "b"]);
        }
        else
        {
            _host.Opened.ShouldBe(["a", "b", "a"]);
        }
    }

    /// <summary>Switching selection cancels cooperative startup, drains its operation, and does not turn cancellation into an error.</summary>
    [Fact]
    public async Task SwitchingSelectionCancelsStartupWithoutAnError()
    {
        AddSessions("a", "b");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _host.OpenGate = (id, token) => string.Equals(id, "a", StringComparison.Ordinal)
            ? opened.Task.WaitAsync(token) : Task.CompletedTask;
        var panel = NewPanel();
        panel.SelectSession("a");
        var firstLoad = panel.SelectionLoad;

        panel.SelectSession("b");
        await panel.SelectionLoad.ConfigureAwait(true);
        await firstLoad.ConfigureAwait(true);

        panel.Selected!.Id.ShouldBe("b");
        _host.ViewModels.ContainsKey("a").ShouldBeFalse();
        _host.OpenRequests[0].Token.IsCancellationRequested.ShouldBeTrue();
        firstLoad.IsCompletedSuccessfully.ShouldBeTrue();
        panel.BannerText.ShouldBeNull();
        panel.State.ShouldBe(PanelState.Ready);
    }

    /// <summary>Both synchronous host throws and delayed startup faults settle SelectionLoad, show a load banner and recover by retrying the same row.</summary>
    /// <param name="delayed">Whether startup fails after an asynchronous boundary.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupFailureIsReportedAndTheSameSelectionCanBeRetried(bool delayed)
    {
        AddSessions("a");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (delayed)
        {
            _host.OpenGate = (_, _) => opened.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            _host.OpenFailure = new IOException("startup failed");
        }

        var panel = NewPanel();
        panel.SelectSession("a").ShouldBeTrue();
        if (delayed)
        {
            opened.SetException(new IOException("startup failed"));
        }

        await panel.SelectionLoad.ConfigureAwait(true);
        panel.SelectionLoad.IsCompletedSuccessfully.ShouldBeTrue();
        panel.Selected.ShouldBeNull();
        panel.State.ShouldBe(PanelState.Error);
        panel.BannerText.ShouldBe("Could not load your agents. startup failed. Your saved agents are not lost.");
        panel.Catalog.SelectedId.ShouldBe("a");

        _host.OpenFailure = null;
        _host.OpenGate = null;
        await panel.RetryLoadingAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        panel.Selected!.Id.ShouldBe("a");
        panel.State.ShouldBe(PanelState.Ready);
        panel.BannerText.ShouldBeNull();
        _host.Opened.ShouldBe(["a", "a"]);
    }

    /// <summary>Clearing selection also drains obsolete startup without activating its model.</summary>
    [Fact]
    public async Task ClearingSelectionTracksTheCancelledOpenUntilItSettles()
    {
        AddSessions("a");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ScriptedContextSource.Returning(Context());
        _host.OpenGate = (_, _) => opened.Task.WaitAsync(TestContext.Current.CancellationToken);
        _host.ContextSources = _ => source;
        var panel = NewPanel();
        panel.SelectSession("a");

        panel.SelectSession(null).ShouldBeTrue();
        var cleared = panel.SelectionLoad;
        panel.Selected.ShouldBeNull();
        _host.OpenRequests.Single().Token.IsCancellationRequested.ShouldBeTrue();
        cleared.IsCompleted.ShouldBeFalse();
        opened.SetResult();
        await cleared.ConfigureAwait(true);

        source.Requests.ShouldBeEmpty();
        panel.Selected.ShouldBeNull();
        panel.PipelineAnnouncement.ShouldBeNull();
        cleared.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>A model returned after disposal is released before SelectionLoad settles, with no activation or publication.</summary>
    [Fact]
    public async Task DisposalReleasesALateSuccessfulOpenWithoutActivatingIt()
    {
        AddSessions("a");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ScriptedContextSource.Returning(Context());
        _host.OpenGate = (_, _) => opened.Task.WaitAsync(TestContext.Current.CancellationToken);
        _host.ContextSources = _ => source;
        var panel = NewPanel();
        panel.SelectSession("a");
        var load = panel.SelectionLoad;

        panel.Dispose();
        panel.Dispose();
        _host.OpenRequests.Single().Token.IsCancellationRequested.ShouldBeTrue();
        load.IsCompleted.ShouldBeFalse();
        opened.SetResult();
        await load.ConfigureAwait(true);

        panel.Selected.ShouldBeNull();
        source.Requests.ShouldBeEmpty();
        // ActivateAsync is a no-op on a disposed model; this observes release through its public behavior.
        await _host.ViewModels["a"].ActivateAsync().ConfigureAwait(true);
        source.Requests.ShouldBeEmpty();
        load.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>A removed row's late model is disposed; adding the same id later opens a fresh model.</summary>
    [Fact]
    public async Task RemovingAPendingSessionDisposesItsLateModel()
    {
        AddSessions("a");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ScriptedContextSource.Returning(Context());
        _host.OpenGate = (_, _) => opened.Task.WaitAsync(TestContext.Current.CancellationToken);
        _host.ContextSources = _ => source;
        var panel = NewPanel();
        panel.SelectSession("a");

        _catalog.Remove("a");
        var removed = panel.SelectionLoad;
        _host.OpenRequests.Single().Token.IsCancellationRequested.ShouldBeTrue();
        panel.Selected.ShouldBeNull();
        opened.SetResult();
        await removed.ConfigureAwait(true);
        var obsolete = _host.ViewModels["a"];
        await obsolete.ActivateAsync().ConfigureAwait(true);
        source.Requests.ShouldBeEmpty();

        _host.OpenGate = null;
        AddSessions("a");
        panel.SelectSession("a");
        await panel.SelectionLoad.ConfigureAwait(true);
        panel.Selected.ShouldNotBeSameAs(obsolete);
        source.Requests.Count.ShouldBe(1);
        _host.Opened.ShouldBe(["a", "a"]);
    }

    /// <summary>Disposal cancels context activation, and the existing SelectionLoad remains the drain for that operation.</summary>
    [Fact]
    public async Task DisposalCancelsAndDrainsContextActivation()
    {
        AddSessions("a");
        var context = new TaskCompletionSource<TaskContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new ScriptedContextSource((_, token) => context.Task.WaitAsync(token));
        _host.ContextSources = _ => source;
        var panel = NewPanel();
        panel.SelectSession("a");
        var load = panel.SelectionLoad;
        load.IsCompleted.ShouldBeFalse();

        panel.Dispose();
        await load.ConfigureAwait(true);

        source.Requests.Single().Token.IsCancellationRequested.ShouldBeTrue();
        load.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Disposal keeps the captured latest load stable, drains A-B-A startup and releases its late successful model without activation.</summary>
    [Fact]
    public async Task DisposalDrainsAPendingReselectionAndReleasesItsLateModel()
    {
        AddSessions("a", "b");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ScriptedContextSource.Returning(Context());
        _host.OpenGate = (id, _) => string.Equals(id, "a", StringComparison.Ordinal) ? opened.Task : Task.CompletedTask;
        _host.ContextSources = id => string.Equals(id, "a", StringComparison.Ordinal) ? source : null;
        var panel = NewPanel();
        panel.SelectSession("a");
        var first = panel.SelectionLoad;
        panel.SelectionLoad.ShouldBeSameAs(first);
        panel.SelectSession("b");
        var second = panel.SelectionLoad;
        second.ShouldNotBeSameAs(first);
        panel.SelectSession("a");
        var latest = panel.SelectionLoad;
        latest.ShouldNotBeSameAs(first);
        latest.ShouldNotBeSameAs(second);
        panel.SelectionLoad.ShouldBeSameAs(latest);

        panel.Dispose();
        panel.Dispose();
        panel.SelectionLoad.ShouldBeSameAs(latest);
        latest.IsCompleted.ShouldBeFalse();
        first.IsCompleted.ShouldBeFalse();
        second.IsCompleted.ShouldBeFalse();
        // b's startup had already completed before the reselection, so only the still-outstanding a startup can be cancelled here.
        _host.OpenRequests.Where(request => string.Equals(request.Id, "a", StringComparison.Ordinal))
            .ShouldAllBe(request => request.Token.IsCancellationRequested);
        opened.SetResult();

        await latest.ConfigureAwait(true);
        await first.ConfigureAwait(true);
        await second.ConfigureAwait(true);
        panel.SelectionLoad.ShouldBeSameAs(latest);
        latest.IsCompletedSuccessfully.ShouldBeTrue();
        first.IsCompletedSuccessfully.ShouldBeTrue();
        second.IsCompletedSuccessfully.ShouldBeTrue();
        _host.Opened.ShouldBe(["a", "b"]);
        panel.Selected.ShouldBeNull();
        panel.BannerText.ShouldBeNull();
        source.Requests.ShouldBeEmpty();
        // ActivateAsync is a no-op after release, proving the late model was disposed.
        await _host.ViewModels["a"].ActivateAsync().ConfigureAwait(true);
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Disposal drains a failed A-B-A startup without retrying or replacing the captured latest load after its token source is disposed.</summary>
    [Fact]
    public async Task DisposalDrainsAFailedPendingReselectionWithoutRetrying()
    {
        AddSessions("a", "b");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ScriptedContextSource.Returning(Context());
        _host.OpenGate = (id, _) => string.Equals(id, "a", StringComparison.Ordinal) ? opened.Task : Task.CompletedTask;
        _host.ContextSources = id => string.Equals(id, "a", StringComparison.Ordinal) ? source : null;
        var panel = NewPanel();
        panel.SelectSession("a");
        var first = panel.SelectionLoad;
        panel.SelectionLoad.ShouldBeSameAs(first);
        panel.SelectSession("b");
        var second = panel.SelectionLoad;
        second.ShouldNotBeSameAs(first);
        panel.SelectSession("a");
        var latest = panel.SelectionLoad;
        latest.ShouldNotBeSameAs(first);
        latest.ShouldNotBeSameAs(second);
        panel.SelectionLoad.ShouldBeSameAs(latest);

        panel.Dispose();
        panel.Dispose();
        panel.SelectionLoad.ShouldBeSameAs(latest);
        latest.IsCompleted.ShouldBeFalse();
        first.IsCompleted.ShouldBeFalse();
        second.IsCompleted.ShouldBeFalse();
        // b's startup had already completed before the reselection, so only the still-outstanding a startup can be cancelled here.
        _host.OpenRequests.Where(request => string.Equals(request.Id, "a", StringComparison.Ordinal))
            .ShouldAllBe(request => request.Token.IsCancellationRequested);
        opened.SetException(new IOException("startup failed after disposal"));

        await latest.ConfigureAwait(true);
        await first.ConfigureAwait(true);
        await second.ConfigureAwait(true);
        panel.SelectionLoad.ShouldBeSameAs(latest);
        latest.IsCompletedSuccessfully.ShouldBeTrue();
        first.IsCompletedSuccessfully.ShouldBeTrue();
        second.IsCompletedSuccessfully.ShouldBeTrue();
        _host.Opened.ShouldBe(["a", "b"]);
        panel.Selected.ShouldBeNull();
        panel.BannerText.ShouldBeNull();
        source.Requests.ShouldBeEmpty();
    }

    /// <summary>Creation does not finish before asynchronous selection startup, for agents and omp terminals alike.</summary>
    /// <param name="terminal">Whether to create an omp terminal rather than an agent.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatingASessionAwaitsItsSelectionStartup(bool terminal)
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _host.OpenGate = (_, token) => opened.Task.WaitAsync(token);
        var panel = NewPanel();

        var create = terminal ? panel.NewTerminalAsync(TestContext.Current.CancellationToken)
            : panel.NewSessionAsync(TestContext.Current.CancellationToken);
        create.IsCompleted.ShouldBeFalse();
        panel.Selected.ShouldBeNull();
        opened.SetResult();
        await create.ConfigureAwait(true);

        panel.Selected!.Id.ShouldBe(terminal ? "terminal-1" : "new-1");
        panel.SelectionLoad.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Removing and re-adding an id while its old startup settles never transfers that removed lifetime to the replacement row.</summary>
    [Fact]
    public async Task ReaddingARowWhileStartupSettlesOpensAFreshModel()
    {
        AddSessions("a");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = ScriptedContextSource.Returning(Context());
        var attempts = 0;
        AgentSessionViewModel? obsolete = null;
        _host.OpenGate = (_, _) =>
        {
            if (++attempts == 1)
            {
                return opened.Task.WaitAsync(TestContext.Current.CancellationToken);
            }

            obsolete = _host.ViewModels["a"];
            return Task.CompletedTask;
        };
        _host.ContextSources = _ => source;
        var panel = NewPanel();
        panel.SelectSession("a");
        _catalog.Remove("a");
        AddSessions("a");
        panel.SelectSession("a");
        var replacement = panel.SelectionLoad;
        _host.Opened.ShouldBe(["a"]);

        opened.SetResult();
        await replacement.ConfigureAwait(true);

        _host.Opened.ShouldBe(["a", "a"]);
        obsolete.ShouldNotBeNull();
        panel.Selected.ShouldNotBeSameAs(obsolete);
        source.Requests.Count.ShouldBe(1);
        await obsolete.ActivateAsync().ConfigureAwait(true);
        source.Requests.Count.ShouldBe(1);
        panel.Selected!.Id.ShouldBe("a");
    }

    /// <summary>A stale startup failure is observed but cannot replace the newer selection's healthy state with an error.</summary>
    [Fact]
    public async Task AnObsoleteStartupFailureDoesNotReportAnErrorForTheLatestSelection()
    {
        AddSessions("a", "b");
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _host.OpenGate = (id, _) => string.Equals(id, "a", StringComparison.Ordinal) ? opened.Task : Task.CompletedTask;
        var panel = NewPanel();
        panel.SelectSession("a");
        panel.SelectSession("b");
        var latest = panel.SelectionLoad;
        panel.Selected!.Id.ShouldBe("b");

        opened.SetException(new IOException("obsolete startup failed"));
        await latest.ConfigureAwait(true);

        panel.Selected.Id.ShouldBe("b");
        panel.State.ShouldBe(PanelState.Ready);
        panel.BannerText.ShouldBeNull();
        latest.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Immediate binding publishes a SelectionLoad that observers can safely capture before selecting another row.</summary>
    [Fact]
    public async Task SelectionObserversSeeTheCurrentLoadBeforeSelectingAnotherSession()
    {
        AddSessions("a", "b");
        var context = new TaskCompletionSource<TaskContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new ScriptedContextSource((_, token) => context.Task.WaitAsync(token));
        _host.ContextSources = id => string.Equals(id, "a", StringComparison.Ordinal) ? source : null;
        var panel = NewPanel();
        Task? firstLoad = null;
        panel.PropertyChanged += (_, change) =>
        {
            if (string.Equals(change.PropertyName, nameof(AgentPanelViewModel.Selected), StringComparison.Ordinal)
                && string.Equals(panel.Selected?.Id, "a", StringComparison.Ordinal))
            {
                firstLoad = panel.SelectionLoad;
                firstLoad.IsCompleted.ShouldBeFalse();
                panel.SelectSession("b");
            }
        };

        panel.SelectSession("a");
        await panel.SelectionLoad.ConfigureAwait(true);

        var observedLoad = firstLoad.ShouldNotBeNull();
        await observedLoad.ConfigureAwait(true);
        source.Requests.Single().Token.IsCancellationRequested.ShouldBeTrue();
        panel.Selected!.Id.ShouldBe("b");
        _host.Opened.ShouldBe(["a", "b"]);
    }

    private AgentPanelViewModel NewPanel()
    {
        _model = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = id => _host.ClosedIds.Contains(id) });
        _panel = new AgentPanelViewModel(_model, _host);
        return _panel;
    }

    private void AddSessions(params string[] ids)
    {
        var now = _clock.GetUtcNow();
        for (var index = 0; index < ids.Length; index++)
        {
            _catalog.Put(Summary(ids[index], SessionLifecycle.Running, now.AddMinutes(-30), now.AddSeconds(-1 - index), title: $"Agent {ids[index]}"));
        }
    }
}
