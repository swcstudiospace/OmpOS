using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.State;
using PendingApproval = OmpOS.Presentation.Approvals.PendingToolApproval;
using RowStatus = OmpOS.Contracts.Catalog.CatalogRowStatus;
using StoredSession = OmpOS.Contracts.Sessions.Session;

namespace OmpOS.Web.Services;

/// <summary>
/// Everything one session needs in the browser, built over the API: the transcript view model fed from <c>GET /sessions/{id}</c> and the
/// session event stream, the composer with its chips and slash palette, steering, tool approvals, the stage timeline, the task context and the
/// Changes bar. It is the web counterpart of the desktop's session runtime. A feature the API cannot serve between runs (live steering, option
/// changes, an omp terminal) shows its reason; nothing here throws into the UI. No prompt is logged or kept beyond the last one for a retry.
/// </summary>
internal sealed class WebSessionRuntime : IAsyncDisposable
{
    private static readonly string[] SideChannelKinds =
    [
        StreamEventKinds.Stage,
        StreamEventKinds.ConfigOptionUpdate,
        "tool_approval_requested",
        "steer",
        "follow_up",
    ];

    private readonly WebSessionRuntimeOptions _options;
    private readonly IOmpApiClient _api;
    private readonly string _sessionId;
    private readonly bool _isTerminal;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _streamCts;
    private Task? _observation;
    private long? _cursor;
    private long _generation;
    private bool _runActive;
    private bool _statusDriven;
    private bool _started;
    private string? _lastPrompt;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="WebSessionRuntime"/> class. It starts nothing: call <see cref="Start"/>.</summary>
    /// <param name="options">What the workbench supplies.</param>
    internal WebSessionRuntime(WebSessionRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _api = options.Api;
        _sessionId = options.Row.Id;
        _isTerminal = options.Row.IsTerminal;
        _time = options.TimeProvider;

        Session = new SessionViewModel();
        Runs = new RunInputScheduler(new WebRunInput(_api, _sessionId));
        Composer = new ComposerViewModel(StartTurnAsync, StopTurnAsync);
        Stages = new WebStageSource(_api, _sessionId);
        Controls = new WebSessionControls(_api, _sessionId, chip => Composer.ModeChip = chip, chip => Composer.ThinkingChip = chip);
        Changes = new WebChangeReview(_api, _sessionId);
        if (!_isTerminal)
        {
            // A terminal runs its own omp, which owns the model, mode and commands: only a chat session offers the chips and the palette.
            Composer.ModelPicker = Controls.Picker;
            Composer.SlashPalette = Controls.Palette;
            Controls.SetActionHost(new WebSlashActionHost(BuildTargets()));
        }

        ViewModel = new AgentSessionViewModel(new AgentSessionOptions
        {
            Row = options.Row,
            Session = Session,
            Composer = Composer,
            Scheduler = Runs,
            Scope = new StageObservationScope { SessionId = _sessionId, WorkspaceRoot = "web", SessionStateDirectory = "web" },
            TimeProvider = _time,
            TaskContextSource = new WebTaskContextSource(_api),
            LiveSteering = DeliverLiveAsync,
            StageRecheck = (source, cancellationToken) => Stages.RecheckAsync(source, cancellationToken),
            Resume = ResumeAsync,
            Reopen = ReopenAsync,
            ApprovalCoordinator = new ToolApprovalCoordinator(options.Approvals),
        });
    }

    /// <summary>Gets the view model the Agent Panel shows for this session.</summary>
    internal AgentSessionViewModel ViewModel { get; }

    /// <summary>Gets the transcript view model.</summary>
    internal SessionViewModel Session { get; }

    /// <summary>Gets the composer.</summary>
    internal ComposerViewModel Composer { get; }

    /// <summary>Gets the scheduler that routes steering and follow-ups to the active run.</summary>
    internal RunInputScheduler Runs { get; }

    /// <summary>Gets the stage source of the session.</summary>
    internal WebStageSource Stages { get; }

    /// <summary>Gets the model picker, the chips and the palette of the composer.</summary>
    internal WebSessionControls Controls { get; }

    /// <summary>Gets the Changes bar model.</summary>
    internal WebChangeReview Changes { get; }

    /// <summary>Loads the history, follows a run that is in flight, starts the stage and context observation and loads the composer controls. Calling it again does nothing.</summary>
    internal void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_started)
        {
            return;
        }

        _started = true;
        _observation = ViewModel.ObserveAsync(Stages, _lifetime.Token);
        _ = ViewModel.ActivateAsync();
        _ = LoadAsync();
    }

    /// <summary>Sends the last prompt again (the Retry of a recoverable error).</summary>
    /// <returns>A task that completes when the prompt was submitted or refused.</returns>
    internal async Task RetryAsync()
    {
        if (_lastPrompt is { } prompt && !Composer.IsStreaming)
        {
            _ = await SubmitAsync(prompt).ConfigureAwait(true);
        }
    }

    /// <summary>Applies a stage the user event stream carried for this session.</summary>
    /// <param name="stage">The stage.</param>
    internal void ApplyStage(StageEvent stage) => Stages.Publish(stage);

    /// <summary>Reacts to the status the user event stream reported: a run another tab started is followed, and a pending approval is listed.</summary>
    /// <param name="rowStatus">The new rail status.</param>
    /// <returns>A task that completes when the reaction ended.</returns>
    internal async Task ApplyStatusAsync(RowStatus rowStatus)
    {
        if (_isTerminal || !_started)
        {
            return;
        }

        if (rowStatus is RowStatus.Running or RowStatus.NeedsApproval or RowStatus.WaitingForYou
            && !_runActive
            && Session.State is not SessionViewState.LoadingHistory)
        {
            // The run was started elsewhere (another tab) or a status event outran this tab's own end of the run: follow it, and let a later
            // status that says the run is over end the following, because no terminal event will arrive on a log that already ended.
            _statusDriven = true;
            BeginRun();
            _ = FollowAsync();
        }
        else if (rowStatus is RowStatus.Completed or RowStatus.Failed or RowStatus.Paused or RowStatus.Closed or RowStatus.Ready or RowStatus.Lost
            && _runActive
            && _statusDriven)
        {
            if (_streamCts is { } active)
            {
                await active.CancelAsync().ConfigureAwait(true);
            }
        }

        if (rowStatus == RowStatus.NeedsApproval)
        {
            await RefreshApprovalsAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Lists the pending tool approvals again and shows them on the view model. Decided entries stay until the next run starts.</summary>
    /// <returns>A task that completes when the list was updated.</returns>
    internal async Task RefreshApprovalsAsync()
    {
        PendingToolApprovalsResponse response;
        try
        {
            response = await _api.GetToolApprovalsAsync(_sessionId, _lifetime.Token).ConfigureAwait(true);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault) || fault is OperationCanceledException)
        {
            return;
        }

        var approvals = ViewModel.PendingApprovals;
        for (var index = approvals.Count - 1; index >= 0; index--)
        {
            var shown = approvals[index];
            if (shown.Status == ToolApprovalStatus.Pending && !response.Approvals.Any(pending => string.Equals(pending.RequestId, shown.RequestId, StringComparison.Ordinal)))
            {
                approvals.RemoveAt(index);
            }
        }

        foreach (var pending in response.Approvals)
        {
            if (!approvals.Any(shown => string.Equals(shown.RequestId, pending.RequestId, StringComparison.Ordinal)))
            {
                approvals.Add(new PendingApproval(_sessionId, pending.RequestId, pending.ToolName, SummaryOf(pending)));
            }
        }

        if (approvals.Any(static shown => shown.Status == ToolApprovalStatus.Pending) && Session.State == SessionViewState.Streaming)
        {
            Session.MarkAwaitingApproval();
        }
    }

    /// <summary>Stops following the stream, cancels the observation and disposes the Changes bar. A second call does nothing.</summary>
    /// <returns>A task that completes when the observation ended.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_streamCts is { } active)
        {
            await active.CancelAsync().ConfigureAwait(true);
        }
        await _lifetime.CancelAsync().ConfigureAwait(true);
        if (_observation is not null)
        {
            try
            {
                await _observation.WaitAsync(TimeSpan.FromSeconds(2), _time).ConfigureAwait(true);
            }
            catch (TimeoutException)
            {
                // A source that ignores cancellation must not hold the head's shutdown.
            }
        }

        Changes.Dispose();
        _streamCts?.Dispose();
        _lifetime.Dispose();
    }

    private static string SummaryOf(Contracts.Controls.PendingToolApproval pending) =>
        pending.GrantableTools.Count == 0 ? string.Empty : "Can also grant: " + string.Join(", ", pending.GrantableTools);

    private static bool IsSideChannel(StreamEventPayload payload) =>
        payload is UnknownPayload { OriginalKind: { } kind } && SideChannelKinds.Contains(kind, StringComparer.Ordinal);

    private WebSlashTargets BuildTargets() => new()
    {
        OpenSettings = _options.Targets?.OpenSettings,
        NewChat = _options.Targets?.NewChat,
        SetMode = Controls.SetModeAsync,
        PickModel = Controls.PickModelAsync,
        ShowHelp = text => Composer.Draft = "/" + text,
    };

    private void Announce(string sentence) => ViewModel.Timeline.Announce(sentence);

    private async Task LoadAsync()
    {
        if (_isTerminal)
        {
            Session.MarkHistoryLoaded();
            return;
        }

        await LoadHistoryAsync().ConfigureAwait(true);
        await Controls.LoadAsync(_lifetime.Token).ConfigureAwait(true);
        await Changes.RefreshAsync(_lifetime.Token).ConfigureAwait(true);
    }

    private async Task LoadHistoryAsync()
    {
        StoredSession stored;
        try
        {
            stored = await _api.GetSessionAsync(_sessionId, _lifetime.Token).ConfigureAwait(true);
        }
        catch (ApiProblemException problem) when (problem.Kind == ApiProblemKind.NotFound)
        {
            Session.MarkHistoryLoaded();
            ReportError("That session no longer exists.", retryable: false);
            return;
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault) || fault is OperationCanceledException)
        {
            Session.MarkOffline();
            return;
        }

        var seen = new SeqTracker();
        foreach (var evt in stored.Events.OrderBy(static e => e.Seq))
        {
            if (!seen.TryAdd(evt.Seq))
            {
                continue;
            }

            if (!IsSideChannel(evt.Payload))
            {
                Session.ApplyEvent(evt);
            }

            _cursor = evt.Seq;
        }

        Session.MarkHistoryLoaded();
        if (stored.Status == SessionStatus.AwaitingToolApproval)
        {
            // The stored status is the only authority for the awaiting-approval state.
            Session.MarkAwaitingApproval();
            await RefreshApprovalsAsync().ConfigureAwait(true);
        }

        if (CatalogMapper.IsRunActive(stored.Status))
        {
            BeginRun();
            _ = FollowAsync();
        }
    }

    private void BeginRun()
    {
        if (!_runActive)
        {
            _runActive = true;
            try
            {
                Runs.RunStarted();
            }
            catch (InvalidOperationException)
            {
                // The scheduler is already dispatching a queued follow-up: the run is active as far as it is concerned.
            }

            RemoveDecidedApprovals();
        }

        Composer.IsStreaming = true;
    }

    private void RemoveDecidedApprovals()
    {
        var approvals = ViewModel.PendingApprovals;
        for (var index = approvals.Count - 1; index >= 0; index--)
        {
            if (approvals[index].Status != ToolApprovalStatus.Pending)
            {
                approvals.RemoveAt(index);
            }
        }
    }

    private async Task EndRunAsync()
    {
        _statusDriven = false;
        Composer.IsStreaming = false;
        if (_runActive)
        {
            _runActive = false;
            try
            {
                await Runs.RunCompletedAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception fault) when (WebFailureText.IsExpected(fault) || fault is InvalidOperationException)
            {
                Announce("A queued message could not be sent. " + WebFailureText.Describe(fault));
            }
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        await RefreshApprovalsAsync().ConfigureAwait(true);
        await Changes.RefreshAsync(_lifetime.Token).ConfigureAwait(true);
        await Controls.LoadAsync(_lifetime.Token).ConfigureAwait(true);
    }

    private async Task FollowAsync()
    {
        var generation = ++_generation;
        if (_streamCts is { } active)
        {
            await active.CancelAsync().ConfigureAwait(true);
        }
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _streamCts = cts;
        var token = cts.Token;
        var stopped = false;
        try
        {
            await foreach (var evt in _api.StreamSessionAsync(_sessionId, _cursor is { } cursor ? cursor + 1 : null, token).ConfigureAwait(true))
            {
                await ApplyLiveAsync(evt).ConfigureAwait(true);
                _cursor = evt.Seq;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            stopped = true;
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            Session.MarkOffline();
            Announce(WebFailureText.Describe(fault));
        }
        finally
        {
            if (generation == _generation)
            {
                if (stopped && Volatile.Read(ref _disposed) == 0)
                {
                    // Stop only ends the following: the transcript returns to its resting state.
                    Session.MarkHistoryLoaded();
                }

                await EndRunAsync().ConfigureAwait(true);
            }

            if (ReferenceEquals(_streamCts, cts))
            {
                _streamCts = null;
            }

            cts.Dispose();
        }
    }

    private async Task ApplyLiveAsync(StreamEvent evt)
    {
        if (!IsSideChannel(evt.Payload))
        {
            Session.ApplyEvent(evt);
        }

        if (evt.Payload is not UnknownPayload { OriginalKind: { } kind } unknown)
        {
            return;
        }

        switch (kind)
        {
            case StreamEventKinds.Stage when StreamEventPayloads.TryReadStage(unknown, out var stage) && stage is not null:
                Stages.Publish(stage);
                break;
            case StreamEventKinds.ConfigOptionUpdate when StreamEventPayloads.TryReadConfigOptionUpdate(unknown, out var update) && update is not null:
                Controls.Apply(update);
                break;
            case "tool_approval_requested":
            case "tool_approval_approved":
            case "tool_approval_denied":
                await RefreshApprovalsAsync().ConfigureAwait(true);
                break;
            default:
                break;
        }
    }

    private void ReportError(string message, bool retryable) =>
        Session.ApplyEvent(new StreamEvent
        {
            Seq = _cursor ?? 0,
            SessionId = _sessionId,
            CorrelationId = _sessionId,
            Timestamp = _time.GetUtcNow(),
            Payload = new ErrorPayload { Message = message, Retryable = retryable },
        });

    private async Task<bool> StartTurnAsync(string text)
    {
        if (_isTerminal)
        {
            Announce("An omp terminal has no prompt. It is not available in the web head.");
            return false;
        }

        if (ViewModel.IsClosed || ViewModel.IsLost)
        {
            Announce(ViewModel.IsClosed ? "This agent is closed. Reopen it to send a message." : "This agent was lost. Resume it to send a message.");
            return false;
        }

        // A slash command is resolved first: a native action runs in the head and sends nothing, an engine command or a skill becomes the prompt.
        if (text.Length > 0 && text[0] is '/' or '&')
        {
            var dispatch = await Controls.DispatchAsync(text, _lifetime.Token).ConfigureAwait(true);
            if (dispatch.Kind == SlashDispatchKind.Handled)
            {
                Announce(dispatch.Text);
                return true;
            }

            text = dispatch.Text;
        }

        return await SubmitAsync(text).ConfigureAwait(true);
    }

    private async Task<bool> SubmitAsync(string text)
    {
        _lastPrompt = text;
        try
        {
            await _api.SubmitPromptAsync(_sessionId, text, _lifetime.Token).ConfigureAwait(true);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            ReportError(WebFailureText.Describe(fault), retryable: true);
            return false;
        }

        BeginRun();
        _ = FollowAsync();
        return true;
    }

    private async Task StopTurnAsync()
    {
        if (!Composer.IsStreaming)
        {
            return;
        }

        if (_streamCts is { } active)
        {
            await active.CancelAsync().ConfigureAwait(true);
        }

        Announce("Stopped following the response. The agent may still be running on the server.");
    }

    private async Task<SteeringDelivery> DeliverLiveAsync(string text, SteeringMode mode, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _api.SteerAsync(_sessionId, text, mode == SteeringMode.FollowUp ? SteerMode.Queue : SteerMode.SendNow, cancellationToken).ConfigureAwait(true);
            return response.Delivery switch
            {
                SteerDelivery.Delivered => new SteeringDelivery(SteeringDeliveryStatus.DeliveredIntoTurn),
                SteerDelivery.Queued => new SteeringDelivery(SteeringDeliveryStatus.QueuedBehindTurn),
                SteerDelivery.Held => new SteeringDelivery(SteeringDeliveryStatus.HeldUntilTurnEnds),
                _ => SteeringDelivery.Failed(string.IsNullOrWhiteSpace(response.Reason) ? "The agent did not take the message." : response.Reason),
            };
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            return SteeringDelivery.Failed(WebFailureText.Describe(fault));
        }
    }

    private async Task ReopenAsync(CancellationToken cancellationToken)
    {
        var response = await _api.ReopenSessionAsync(_sessionId, cancellationToken).ConfigureAwait(true);
        _ = _options.Catalog.Apply(response);
    }

    private async Task ResumeAsync(CancellationToken cancellationToken)
    {
        var response = await _api.ResumeSessionAsync(_sessionId, cancellationToken).ConfigureAwait(true);
        _ = _options.Catalog.Apply(response);
        await LoadHistoryAsync().ConfigureAwait(true);
    }
}
