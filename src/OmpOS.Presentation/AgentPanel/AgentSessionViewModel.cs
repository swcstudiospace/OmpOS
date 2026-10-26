using System.Collections.ObjectModel;
using System.ComponentModel;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Observable;
using OmpOS.Presentation.State;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// Everything the Agent Panel shows for one session: it composes the existing <see cref="SessionViewModel"/> (transcript),
/// <see cref="ComposerViewModel"/> and run scheduler (through <see cref="SteeringInputModel"/>) with the pipeline timeline and the task
/// context, and derives the thread header: status, steering badge, notice, run summary and the close, resume and reopen commands.
/// </summary>
/// <remarks>
/// <para>
/// The model owns no engine. A head builds it from <see cref="AgentSessionOptions"/>, pumps stage events into it
/// (<see cref="ApplyStageEvent"/> or <see cref="ObserveAsync"/>) and reports closing and reopening (<see cref="MarkClosed"/>,
/// <see cref="MarkReopened"/>). The panel view model calls <see cref="ActivateAsync"/> and <see cref="Deactivate"/> when the selection
/// moves, so a session that is not shown holds no running load.
/// </para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class AgentSessionViewModel : ObservableModel, IDisposable
{
    private readonly AgentSessionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly AgentPanelCommand _closeCommand;
    private readonly AgentPanelCommand _resumeCommand;
    private readonly AgentPanelCommand _reopenCommand;
    private readonly AgentPanelCommand _dismissNoticeCommand;
    private AgentRow _row;
    private bool _closed;
    private bool _resumed;
    private bool _noPluginsDismissed;
    private int _dismissedDegradedCount;
    private SessionViewState _lastSessionState;
    private string? _actionError;
    private bool _disposed;

    /// <summary>Creates the view model of one session and wires its parts together.</summary>
    /// <param name="options">What the head supplies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public AgentSessionViewModel(AgentSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _timeProvider = options.TimeProvider ?? TimeProvider.System;
        _row = options.Row;
        _closed = options.Row.Status == AgentRowStatus.Closed;
        Session = options.Session;
        Composer = options.Composer;
        Timeline = new StageTimelineModel(options.Row.Id, _timeProvider, options.StageRecheck);
        Context = new TaskContextModel(options.TaskContextSource, _timeProvider);
        Steering = new SteeringInputModel(options.Scheduler, _timeProvider, options.SteeringRecorder);
        PendingApprovals = [];
        _lastSessionState = Session.State;
        _closeCommand = new AgentPanelCommand(
            _ =>
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            },
            static _ => { },
            _ => !_closed);
        _resumeCommand = new AgentPanelCommand(_ => ResumeAsync(), ReportActionFault, _ => IsLost && _options.Resume is not null);
        _reopenCommand = new AgentPanelCommand(_ => ReopenAsync(), ReportActionFault, _ => _closed && _options.Reopen is not null);
        _dismissNoticeCommand = new AgentPanelCommand(
            _ =>
            {
                DismissNotice();
                return Task.CompletedTask;
            },
            static _ => { },
            _ => NoticeKind is not (PanelNoticeKind.None or PanelNoticeKind.Lost));
        Timeline.PropertyChanged += OnTimelineChanged;
        Context.PropertyChanged += OnContextChanged;
        Steering.PropertyChanged += OnSteeringChanged;
        Session.PropertyChanged += OnSessionChanged;
    }

    /// <summary>Occurs when the user asks to close the session through <see cref="CloseCommand"/>. The owner of the engines closes it.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets the session identifier.</summary>
    public string Id => _row.Id;

    /// <summary>Gets the display title.</summary>
    public string Title => _row.Title;

    /// <summary>Gets the rail row of the session: its status, group, last activity and flags.</summary>
    public AgentRow Row => _row;

    /// <summary>Gets the scope the session's stage sources and context source read.</summary>
    public StageObservationScope Scope => _options.Scope;

    /// <summary>Gets the transcript view model.</summary>
    public SessionViewModel Session { get; }

    /// <summary>Gets the composer.</summary>
    public ComposerViewModel Composer { get; }

    /// <summary>Gets the pipeline timeline.</summary>
    public StageTimelineModel Timeline { get; }

    /// <summary>Gets the task context.</summary>
    public TaskContextModel Context { get; }

    /// <summary>Gets the steering input and history.</summary>
    public SteeringInputModel Steering { get; }

    /// <summary>Gets the tool approvals that wait for a decision, in the order the run raised them. The head fills it.</summary>
    public ObservableCollection<PendingToolApproval> PendingApprovals { get; }

    /// <summary>Gets the coordinator that sends approval decisions, or null when none is bound.</summary>
    public ToolApprovalCoordinator? ApprovalCoordinator => _options.ApprovalCoordinator;

    /// <summary>Gets the title of the task the session works on, or null until the task context loaded.</summary>
    public string? TaskTitle => Context.HasCard ? Context.Title : null;

    /// <summary>Gets the Graph identifier of the session, from the stage events, the task context or the catalog, or null.</summary>
    public string? GraphId => _row.GraphId ?? Context.GraphId;

    /// <summary>Gets whether the session was closed: its engine stopped. The composer is hidden and <see cref="ReopenCommand"/> is offered.</summary>
    public bool IsClosed => _closed;

    /// <summary>Gets whether the session was running when the application closed. <see cref="ResumeCommand"/> is offered.</summary>
    public bool IsLost => _row.IsLost;

    /// <summary>Gets the steering badge text for the thread header, one of the four texts of the Copywriting Contract.</summary>
    public string SteeringBadgeText
    {
        get
        {
            if (_closed || IsLost || _row.Status == AgentRowStatus.Failed || Session.State == SessionViewState.FatalError)
            {
                return AgentPanelCopy.SteeringUnavailable;
            }

            if (!Steering.IsRunActive)
            {
                return AgentPanelCopy.SteeringIdle;
            }

            return Steering.IsRecordingAvailable ? AgentPanelCopy.SteeringAvailable : AgentPanelCopy.SteeringAvailableNotRecorded;
        }
    }

    /// <summary>Gets the accessible name of the steering badge.</summary>
    public string SteeringBadgeName => AgentPanelCopy.SteeringBadgeName(SteeringBadgeText);

    /// <summary>Gets which notice the thread header shows.</summary>
    public PanelNoticeKind NoticeKind
    {
        get
        {
            if (_resumed)
            {
                return PanelNoticeKind.Resumed;
            }

            if (IsLost)
            {
                return PanelNoticeKind.Lost;
            }

            if (Timeline.AllPluginsUnavailable)
            {
                return _noPluginsDismissed ? PanelNoticeKind.None : PanelNoticeKind.NoPlugins;
            }

            return Timeline.DegradedStageCount > _dismissedDegradedCount ? PanelNoticeKind.Degraded : PanelNoticeKind.None;
        }
    }

    /// <summary>Gets the text of the notice, or null when there is none.</summary>
    public string? NoticeText => NoticeKind switch
    {
        PanelNoticeKind.Degraded => AgentPanelCopy.DegradedNotice(Timeline.DegradedStageCount),
        PanelNoticeKind.NoPlugins => AgentPanelCopy.NoPlugins,
        PanelNoticeKind.Lost => $"{AgentPanelCopy.LostHeading} {AgentPanelCopy.LostBody}",
        PanelNoticeKind.Resumed => AgentPanelCopy.Resumed,
        _ => null,
    };

    /// <summary>Gets whether a notice is shown.</summary>
    public bool HasNotice => NoticeKind != PanelNoticeKind.None;

    /// <summary>
    /// Gets the run summary sentence, <c>Finished. Ultrathink: planned. Swarm: 5 of 5 done. ...</c>, while the transcript is in its success
    /// state; null otherwise. It is built from the stage events and the recorded steering messages and never invents a count.
    /// </summary>
    public string? RunSummaryText => Session.State == SessionViewState.Success ? BuildSummary("Finished.") : null;

    /// <summary>Gets whether closing the session needs the confirmation bar: a run is active or a tool approval waits.</summary>
    public bool RequiresCloseConfirmation => Steering.IsRunActive || Session.State is SessionViewState.Streaming or SessionViewState.AwaitingApproval;

    /// <summary>Gets the text of the close confirmation bar for this session.</summary>
    public string CloseConfirmationText => AgentPanelCopy.CloseConfirmation(Title);

    /// <summary>Gets the text of the last failed resume or reopen, or null. The next successful action clears it.</summary>
    public string? ActionError => _actionError;

    /// <summary>Gets the command that asks to close the session. The view shows the confirmation bar first when <see cref="RequiresCloseConfirmation"/>.</summary>
    public AgentPanelCommand CloseCommand => _closeCommand;

    /// <summary>Gets the command that resumes a lost session.</summary>
    public AgentPanelCommand ResumeCommand => _resumeCommand;

    /// <summary>Gets the command that reopens a closed session.</summary>
    public AgentPanelCommand ReopenCommand => _reopenCommand;

    /// <summary>Gets the command that dismisses the current notice. A lost session's notice stays until it is resumed.</summary>
    public AgentPanelCommand DismissNoticeCommand => _dismissNoticeCommand;

    /// <summary>Takes the latest catalog row of the session: its title and status.</summary>
    /// <param name="row">The row. Rows of another session are ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public void ApplyRow(AgentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!string.Equals(row.Id, _row.Id, StringComparison.Ordinal) || row == _row)
        {
            return;
        }

        _row = row;
        if (row.Status == AgentRowStatus.Closed)
        {
            _closed = true;
        }

        NotifyPropertyChanged(nameof(Row));
        NotifyPropertyChanged(nameof(Title));
        NotifyPropertyChanged(nameof(GraphId));
        NotifyHeader();
    }

    /// <summary>Folds one stage event into the timeline.</summary>
    /// <param name="stageEvent">The event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stageEvent"/> is null.</exception>
    public void ApplyStageEvent(StageEvent stageEvent) => Timeline.Apply(stageEvent);

    /// <summary>
    /// Folds the events of a stage source into the timeline until the source ends or <paramref name="cancellationToken"/> is cancelled.
    /// A fault of the source becomes a degraded engine stage with fixed text and never reaches the caller, because a plugin must not take
    /// the panel down.
    /// </summary>
    /// <param name="source">The source to observe, scoped to this session.</param>
    /// <param name="cancellationToken">Ends the observation.</param>
    /// <returns>A task that completes when the observation ended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public async Task ObserveAsync(IStageSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            await foreach (var stageEvent in source.ObserveAsync(_options.Scope, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(true))
            {
                Timeline.Apply(stageEvent);
            }
        }
        catch (OperationCanceledException)
        {
            // The caller ended the observation.
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            Timeline.Apply(new StageEvent
            {
                SessionId = _row.Id,
                Source = StageSource.Omp,
                StageId = "stage-observation",
                Title = "Stage updates",
                Status = StageStatus.Degraded,
                Timestamp = _timeProvider.GetUtcNow(),
                Detail = "Stage updates stopped. The agent keeps running.",
            });
        }
    }

    /// <summary>Loads the task context of this session. The panel calls it when the session is selected.</summary>
    /// <returns>A task that completes when the load ended or was superseded. It never faults.</returns>
    public Task ActivateAsync() => Context.SelectAsync(_options.Scope);

    /// <summary>Cancels the running context load. The panel calls it when another session is selected.</summary>
    public void Deactivate() => Context.Cancel();

    /// <summary>Records that the session's engine was stopped by closing it.</summary>
    public void MarkClosed()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        NotifyPropertyChanged(nameof(IsClosed));
        NotifyHeader();
    }

    /// <summary>Records that the session's engine runs again.</summary>
    public void MarkReopened()
    {
        if (!_closed)
        {
            return;
        }

        _closed = false;
        NotifyPropertyChanged(nameof(IsClosed));
        NotifyHeader();
    }

    /// <summary>Unsubscribes from the parts and cancels the context load.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Timeline.PropertyChanged -= OnTimelineChanged;
        Context.PropertyChanged -= OnContextChanged;
        Steering.PropertyChanged -= OnSteeringChanged;
        Session.PropertyChanged -= OnSessionChanged;
        Context.Dispose();
        Steering.Dispose();
    }

    private async Task ResumeAsync()
    {
        var resume = _options.Resume;
        if (resume is null)
        {
            return;
        }

        await resume(CancellationToken.None).ConfigureAwait(true);
        SetActionError(null);
        _resumed = true;
        Timeline.Announce(AgentPanelCopy.ResumedAnnouncement);
        NotifyHeader();
    }

    private async Task ReopenAsync()
    {
        var reopen = _options.Reopen;
        if (reopen is null)
        {
            return;
        }

        await reopen(CancellationToken.None).ConfigureAwait(true);
        SetActionError(null);
        MarkReopened();
    }

    private void ReportActionFault(Exception fault)
    {
        var reason = PanelText.Plain(fault.Message, 200);
        SetActionError(AgentPanelCopy.ActionFailed(IsLost ? "resume this agent" : "reopen this agent", reason));
    }

    private void SetActionError(string? text) => SetProperty(ref _actionError, text, nameof(ActionError));

    private void DismissNotice()
    {
        switch (NoticeKind)
        {
            case PanelNoticeKind.Resumed:
                _resumed = false;
                break;
            case PanelNoticeKind.NoPlugins:
                _noPluginsDismissed = true;
                break;
            case PanelNoticeKind.Degraded:
                _dismissedDegradedCount = Timeline.DegradedStageCount;
                break;
            default:
                return;
        }

        NotifyNotice();
    }

    private string BuildSummary(string prefix)
    {
        var body = Timeline.BuildRunSummary(Steering.RecordedCount);
        return body.Length == 0 ? prefix : $"{prefix} {body}";
    }

    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(StageTimelineModel.Blocks):
                SyncSubstrate();
                SyncNoticeDismissals();
                NotifyNotice();
                NotifyPropertyChanged(nameof(RunSummaryText));
                break;
            case nameof(StageTimelineModel.Phase):
                NotifyPropertyChanged(nameof(RunSummaryText));
                break;
            default:
                break;
        }
    }

    private void OnContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TaskContextModel.Phase):
                Timeline.SetPhase(Context.Phase);
                break;
            case nameof(TaskContextModel.HasCard):
            case nameof(TaskContextModel.Title):
                NotifyPropertyChanged(nameof(TaskTitle));
                break;
            case nameof(TaskContextModel.GraphId):
                NotifyPropertyChanged(nameof(GraphId));
                break;
            default:
                break;
        }
    }

    private void OnSteeringChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SteeringInputModel.IsRunActive):
            case nameof(SteeringInputModel.IsRecordingAvailable):
                NotifyPropertyChanged(nameof(SteeringBadgeText));
                NotifyPropertyChanged(nameof(SteeringBadgeName));
                NotifyPropertyChanged(nameof(RequiresCloseConfirmation));
                break;
            case nameof(SteeringInputModel.RecordedCount):
                NotifyPropertyChanged(nameof(RunSummaryText));
                break;
            default:
                break;
        }
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SessionViewModel.State), StringComparison.Ordinal))
        {
            return;
        }

        var previous = _lastSessionState;
        _lastSessionState = Session.State;
        NotifyPropertyChanged(nameof(SteeringBadgeText));
        NotifyPropertyChanged(nameof(SteeringBadgeName));
        NotifyPropertyChanged(nameof(RequiresCloseConfirmation));
        NotifyPropertyChanged(nameof(RunSummaryText));
        if (Session.State == SessionViewState.Success
            && previous is SessionViewState.Streaming or SessionViewState.AwaitingApproval or SessionViewState.Reconnecting)
        {
            Timeline.FlushPending();
            Timeline.Announce(BuildSummary(AgentPanelCopy.AgentFinished));
        }
    }

    private void SyncSubstrate()
    {
        bool? reachable = Timeline.StatusOf(StageSource.Substrate) switch
        {
            StageStatus.Succeeded or StageStatus.Degraded => true,
            StageStatus.Failed or StageStatus.Unavailable or StageStatus.Skipped => false,
            _ => null,
        };
        Context.SubstrateAvailable = reachable;
        Steering.IsSubstrateReachable = reachable != false;
    }

    private void SyncNoticeDismissals()
    {
        if (!Timeline.AllPluginsUnavailable)
        {
            _noPluginsDismissed = false;
        }

        _dismissedDegradedCount = Math.Min(_dismissedDegradedCount, Timeline.DegradedStageCount);
    }

    private void NotifyNotice()
    {
        NotifyPropertyChanged(nameof(NoticeKind));
        NotifyPropertyChanged(nameof(NoticeText));
        NotifyPropertyChanged(nameof(HasNotice));
        _dismissNoticeCommand.NotifyCanExecuteChanged();
    }

    private void NotifyHeader()
    {
        NotifyPropertyChanged(nameof(IsLost));
        NotifyPropertyChanged(nameof(SteeringBadgeText));
        NotifyPropertyChanged(nameof(SteeringBadgeName));
        NotifyNotice();
        _resumeCommand.NotifyCanExecuteChanged();
        _reopenCommand.NotifyCanExecuteChanged();
        _closeCommand.NotifyCanExecuteChanged();
    }
}
