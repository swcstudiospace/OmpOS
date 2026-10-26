using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.RunControls;
using OmpOS.Native.Views.Terminal;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.State;
using OmpOS.Sessions;

namespace OmpOS.Native.Views.Sessions;

/// <summary>
/// Everything the main view does for ONE agent session: it owns the transcript, the composer, the run scheduler with the engine's run
/// input, the tool-approval controls and the session's <see cref="AgentSessionViewModel"/>, sends a prompt to the session's engine,
/// streams the turn into the transcript, runs the messages held behind the active turn in send order, and shows a stop, a retry, a
/// fault and a held message the way the single-session shell always did. A head creates one runtime per session, so sessions keep
/// running while another one is shown.
/// </summary>
/// <remarks>
/// <para>
/// Threading: the runtime runs on the UI context that owns the presentation models, like those models themselves. The stage pump
/// started by <see cref="StartObserving"/> continues on the context that called it.
/// </para>
/// <para>
/// A runtime registers itself so the main view can find the runtime of the session view model it shows (<see cref="For"/>): the panel
/// view model hands the main view only the session view model.
/// </para>
/// </remarks>
public sealed class AgentSessionRuntime : IAsyncDisposable
{
    private static readonly ConditionalWeakTable<AgentSessionViewModel, AgentSessionRuntime> Registry = new();

    /// <summary>How long disposing waits for the stage source to end after it was cancelled.</summary>
    private static readonly TimeSpan ObservationShutdown = TimeSpan.FromSeconds(5);

    /// <summary>How long a steering delivery waits for omp to acknowledge a message before the entry says it was sent and not acknowledged.</summary>
    private static readonly TimeSpan AckWait = TimeSpan.FromSeconds(3);

    private readonly AgentSessionRuntimeOptions _options;
    private readonly EngineRunInput _queued = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TimeProvider _timeProvider;
    private Task? _observation;
    private bool _stopRequested;
    private string? _lastPrompt;
    private long _nextSeq = 1;
    private string _statusText = string.Empty;
    private string? _errorText;
    private int _disposed;
    private bool _terminalClosed;
    private bool _terminalLost;
    private readonly ModelPickerBinding? _modelBinding;
    private readonly OptionChipBinding? _modeBinding;
    private readonly OptionChipBinding? _thinkingBinding;

    /// <summary>Creates the runtime of one session and its view model. It starts no observation: call <see cref="StartObserving"/>.</summary>
    /// <param name="options">What the head supplies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public AgentSessionRuntime(AgentSessionRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _timeProvider = options.TimeProvider ?? TimeProvider.System;

        Session = new SessionViewModel();
        Session.MarkHistoryLoaded();
        RunControls = new RunControlsViewModel(options.Approvals);
        Runs = new RunInputScheduler(_queued);
        Composer = new ComposerViewModel(StartTurnAsync, StopTurnAsync);
        if (options.Models is { } models && options.TerminalStarter is null)
        {
            // A terminal runs its own omp, which owns its model: only a chat session offers the model chip.
            _modelBinding = new ModelPickerBinding(models, options.ConnectModels);
            Composer.ModelPicker = _modelBinding.Picker;
        }

        if (options.SessionOptions is { } sessionOptions && options.TerminalStarter is null)
        {
            // A terminal runs its own omp, which owns its mode and thinking level: only a chat session offers the chips.
            _modeBinding = new OptionChipBinding(sessionOptions, "mode", "Mode", chip => Composer.ModeChip = chip);
            _thinkingBinding = new OptionChipBinding(sessionOptions, "thinking", "Thinking", chip => Composer.ThinkingChip = chip);
        }

        ViewModel = new AgentSessionViewModel(new AgentSessionOptions
        {
            Row = options.Row,
            Session = Session,
            Composer = Composer,
            Scheduler = Runs,
            Scope = options.Scope,
            TimeProvider = options.TimeProvider,
            TaskContextSource = options.Context,
            SteeringRecorder = options.RecordSteering,
            StageRecheck = options.StageRecheck,
            Resume = options.Resume,
            Reopen = options.Reopen,
            ApprovalCoordinator = RunControls.Coordinator,
            LiveSteering = options.Steering is null && options.NativeSteering is null ? null : DeliverLiveAsync,
        });
        RunControls.Approvals.CollectionChanged += OnApprovalsChanged;
        Registry.AddOrUpdate(ViewModel, this);
        if (options.TerminalStarter is { } starter)
        {
            // The runtime owns the terminal for as long as the session lives, so a view that is hidden by a tab or session switch leaves omp running.
            _terminalClosed = ViewModel.IsClosed;
            _terminalLost = ViewModel.IsLost;
            Terminal = new OmpTerminalViewModel(starter) { KeepRunningWhenDetached = true, AutoStart = !_terminalClosed && !_terminalLost };
            Terminal.PropertyChanged += OnTerminalChanged;
            Terminal.CloseRequested += OnTerminalCloseRequested;
            ViewModel.PropertyChanged += OnViewModelChanged;
        }
    }

    /// <summary>Occurs on the UI context when a run started, completed or faulted.</summary>
    public event EventHandler<SessionTurnEventArgs>? TurnChanged;

    /// <summary>Occurs when <see cref="StatusText"/> or <see cref="ErrorText"/> changed.</summary>
    internal event EventHandler? FeedbackChanged;

    /// <summary>Gets the view model of the session that the Agent Panel shows. It is created with the runtime and lives until the panel releases it.</summary>
    public AgentSessionViewModel ViewModel { get; }

    /// <summary>Gets what the session is: a chat over the engine behind the Agent Panel, or an omp terminal.</summary>
    public SessionKind Kind => Terminal is null ? SessionKind.Chat : SessionKind.Terminal;

    /// <summary>
    /// Gets the omp terminal of a terminal runtime, or null for a chat. The view starts omp when it is first shown; the runtime stops it
    /// when the session is closed or the runtime is disposed.
    /// </summary>
    internal OmpTerminalViewModel? Terminal { get; }

    /// <summary>Gets the pending tool-approval requests of the session and the coordinator that sends each decision.</summary>
    internal RunControlsViewModel RunControls { get; }

    /// <summary>Gets the transcript view model of the session.</summary>
    internal SessionViewModel Session { get; }

    /// <summary>Gets the composer of the session.</summary>
    internal ComposerViewModel Composer { get; }

    /// <summary>Gets the scheduler that routes steering messages and queued follow-ups to the active run.</summary>
    internal RunInputScheduler Runs { get; }

    /// <summary>Gets the motion setting the head supplied.</summary>
    public MotionPreference Motion => _options.Motion;

    /// <summary>Gets the composer feedback the status line shows (a refused send, nothing to stop, a faulted action), or an empty string.</summary>
    internal string StatusText => _statusText;

    /// <summary>Gets the engine failure the error banner shows, or null when the last send did not fail.</summary>
    internal string? ErrorText => _errorText;

    /// <summary>Gets the runtime that owns <paramref name="viewModel"/>, or null when the view model was not built by a runtime.</summary>
    /// <param name="viewModel">A session view model.</param>
    /// <returns>The owning runtime, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="viewModel"/> is null.</exception>
    internal static AgentSessionRuntime? For(AgentSessionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return Registry.TryGetValue(viewModel, out var runtime) ? runtime : null;
    }

    /// <summary>
    /// Starts pumping the stage source into <see cref="AgentSessionViewModel.Timeline"/> and loads the task context. A fault of the stage
    /// source becomes a Degraded engine stage and never an unobserved exception. Calling it again does nothing.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The runtime was disposed.</exception>
    public void StartObserving()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (_observation is not null)
        {
            return;
        }

        _observation = PumpStagesAsync(_lifetime.Token);
        _ = ViewModel.ActivateAsync();
    }

    /// <summary>
    /// Stops the turn in flight, if one runs, cancels the stage observation and disposes the send path once. A second call does nothing.
    /// </summary>
    /// <returns>A task that completes when the send path has been disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        RunControls.Approvals.CollectionChanged -= OnApprovalsChanged;
        _modelBinding?.Dispose();
        _modeBinding?.Dispose();
        _thinkingBinding?.Dispose();
        if (Terminal is { } terminal)
        {
            terminal.PropertyChanged -= OnTerminalChanged;
            terminal.CloseRequested -= OnTerminalCloseRequested;
            ViewModel.PropertyChanged -= OnViewModelChanged;
        }

        await _lifetime.CancelAsync().ConfigureAwait(true);
        try
        {
            if (_observation is { } observation)
            {
                await WaitForObservationAsync(observation).ConfigureAwait(true);
            }

            if (Composer.IsStreaming)
            {
                await StopTurnAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            try
            {
                if (Terminal is { } owned)
                {
                    await owned.DisposeAsync().ConfigureAwait(true);
                }
            }
            finally
            {
                try
                {
                    await _options.Prompts.DisposeAsync().ConfigureAwait(true);
                }
                finally
                {
                    _lifetime.Dispose();
                }
            }
        }
    }

    /// <summary>Sends the last prompt again. It does nothing while a response streams and before any prompt was sent.</summary>
    internal void RetryLastTurn()
    {
        if (_lastPrompt is not { } prompt || Composer.IsStreaming)
        {
            return;
        }

        _ = StartTurnAsync(prompt);
    }

    /// <summary>Shows a line of composer feedback on the status line. An empty text clears it.</summary>
    /// <param name="text">The feedback text.</param>
    internal void ReportStatus(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _statusText = text;
        FeedbackChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task WaitForObservationAsync(Task observation)
    {
        try
        {
            await observation.WaitAsync(ObservationShutdown, _timeProvider).ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            // A stage source that ignores cancellation must not hold the head's shutdown. The pump ends when the source does.
        }
    }

    private async Task PumpStagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ViewModel.ObserveAsync(_options.Stages, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The runtime was disposed.
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            ViewModel.ApplyStageEvent(new StageEvent
            {
                SessionId = ViewModel.Id,
                Source = StageSource.Omp,
                StageId = "stage-observation",
                Title = "Stage updates",
                Status = StageStatus.Degraded,
                Timestamp = _timeProvider.GetUtcNow(),
                Detail = "Stage updates stopped. The agent keeps running.",
            });
        }
    }

    private void OnApprovalsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // The head fills RunControls.Approvals; the session view model mirrors it for the panel's own bindings.
        var mirror = ViewModel.PendingApprovals;
        mirror.Clear();
        foreach (var approval in RunControls.Approvals)
        {
            mirror.Add(approval);
        }
    }

    private Task<bool> RefuseSendAsync()
    {
        ReportStatus(MainView.RefusedSendStatus);
        return Task.FromResult(false);
    }

    // The run of a terminal runtime is the life of its omp: steering is offered while omp runs and not before or after.
    private void OnTerminalChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Terminal is not { } terminal || !string.Equals(e.PropertyName, nameof(OmpTerminalViewModel.State), StringComparison.Ordinal))
        {
            return;
        }

        var running = terminal.State == OmpTerminalState.Running;
        if (running && !Runs.IsRunActive)
        {
            Runs.RunStarted();
        }
        else if (!running && Runs.IsRunActive)
        {
            _ = Runs.RunCompletedAsync(CancellationToken.None);
        }
    }

    // The terminal's own Close control stopped omp: the session closes like any other, so its card and its row follow.
    private void OnTerminalCloseRequested(object? sender, EventArgs e) => ViewModel.CloseCommand.Execute(null);

    // Closing the session stops omp; reopening or resuming it starts omp again in the view that is already showing the terminal. A session can
    // learn that it was closed from its row (which notifies IsLost) before the panel calls MarkClosed, so the flags are compared, not the name.
    // A session that is closed or lost is not started by showing it: AutoStart is off until it reopens or resumes.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Terminal is not { } terminal
            || e.PropertyName is not (nameof(AgentSessionViewModel.IsClosed) or nameof(AgentSessionViewModel.IsLost)))
        {
            return;
        }

        var wasClosed = _terminalClosed;
        var wasLost = _terminalLost;
        _terminalClosed = ViewModel.IsClosed;
        _terminalLost = ViewModel.IsLost;
        terminal.AutoStart = !_terminalClosed && !_terminalLost;
        if (_terminalClosed && !wasClosed)
        {
            _ = ChangeTerminalAsync(terminal.StopAsync);
        }
        else if (!_terminalClosed && (wasClosed || (wasLost && !_terminalLost)) && terminal.State == OmpTerminalState.Idle)
        {
            _ = ChangeTerminalAsync(terminal.StartAsync);
        }
    }

    private async Task ChangeTerminalAsync(Func<Task> change)
    {
        try
        {
            await change().ConfigureAwait(true);
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            ReportStatus($"{MainView.FaultedStatus} {fault.GetBaseException().Message}");
        }
    }

    /// <summary>
    /// Delivers a steer or a follow-up into the running turn. A chat session whose omp supports live steering hands the message to omp's own
    /// steering (see <see cref="DeliverNativeAsync"/>); a terminal, and a chat session whose omp does not support it, use the omp steering
    /// inbox (see <see cref="DeliverThroughInboxAsync"/>).
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="mode">Steer or follow-up.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>The delivery the steering history records.</returns>
    private Task<SteeringDelivery> DeliverLiveAsync(string text, SteeringMode mode, CancellationToken cancellationToken) =>
        Terminal is null && _options.NativeSteering is { IsSupported: true } native
            ? DeliverNativeAsync(native, text, mode, cancellationToken)
            : DeliverThroughInboxAsync(text, mode, cancellationToken);

    /// <summary>
    /// Delivers a message through omp's own steering and reports what omp answered: injected is Delivered (a follow-up is Queued behind the
    /// running turn), a turn that ended while the user typed makes the message a new turn of its own, and a refusal is a failed delivery with
    /// omp's reason, so the text stays in the composer. An omp that turns out not to support steering gets the message through the inbox.
    /// </summary>
    /// <param name="native">The omp live steering of the session.</param>
    /// <param name="text">The message.</param>
    /// <param name="mode">Steer or follow-up.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>The delivery the steering history records.</returns>
    private async Task<SteeringDelivery> DeliverNativeAsync(IOmpLiveSteering native, string text, SteeringMode mode, CancellationToken cancellationToken)
    {
        var queue = mode == SteeringMode.FollowUp ? OmpSteerLane.FollowUp : OmpSteerLane.Steering;
        OmpSteerResult result;
        try
        {
            result = await native.SteerAsync(text, queue, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception fault) when (fault is not (OperationCanceledException or OutOfMemoryException))
        {
            return FailDelivery($"omp could not take the message: {fault.Message}");
        }

        switch (result.Outcome)
        {
            case OmpSteerOutcome.Injected:
                return new SteeringDelivery(result.Queue == OmpSteerLane.FollowUp ? SteeringDeliveryStatus.QueuedBehindTurn : SteeringDeliveryStatus.DeliveredIntoTurn);
            case OmpSteerOutcome.PromptRequired:
            case OmpSteerOutcome.NotRunning:
                return await SubmitAsNewTurnAsync(text, cancellationToken).ConfigureAwait(true);
            case OmpSteerOutcome.Unsupported:
                return await DeliverThroughInboxAsync(text, mode, cancellationToken).ConfigureAwait(true);
            case OmpSteerOutcome.Failed:
            default:
                return FailDelivery(string.IsNullOrWhiteSpace(result.Reason) ? "omp could not take the message." : result.Reason);
        }
    }

    /// <summary>
    /// Sends a message as a turn of its own because omp had no turn to steer: the turn ended while the user typed. omp consumed nothing and
    /// started nothing, so this runtime owns the new turn. A run that this runtime has not closed yet takes the message as its next turn; a run that
    /// has ended starts a new one. While the run is being stopped nothing is sent and the text stays in the composer.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">Cancels the hand-over.</param>
    /// <returns>The delivery the steering history records.</returns>
    private async Task<SteeringDelivery> SubmitAsNewTurnAsync(string text, CancellationToken cancellationToken)
    {
        if (_stopRequested)
        {
            return FailDelivery("The turn is being stopped, so the message was not sent. Send it again once it has stopped.");
        }

        if (Runs.IsRunActive)
        {
            await _queued.FollowUpAsync(text, cancellationToken).ConfigureAwait(true);
            Runs.NotifySteersHeldChanged();
        }
        else if (!await StartTurnAsync(text).ConfigureAwait(true))
        {
            return SteeringDelivery.Failed(MainView.RefusedSendStatus);
        }

        ReportStatus(AgentPanelCopy.DeliverySubmittedAsNewTurn);
        return new SteeringDelivery(SteeringDeliveryStatus.SubmittedAsNewTurn);
    }

    /// <summary>
    /// Delivers a steer or a follow-up through the omp steering channel and reports what the channel can prove: omp's acknowledgement
    /// decides Delivered, Failed or (no acknowledgement within <see cref="AckWait"/>) Sent but not acknowledged. A channel that cannot take
    /// the directive is a failed delivery with the channel's own plain message, which the status line shows.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="mode">Steer or follow-up.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>The delivery the steering history records.</returns>
    private async Task<SteeringDelivery> DeliverThroughInboxAsync(string text, SteeringMode mode, CancellationToken cancellationToken)
    {
        if (_options.Steering is not { } channel)
        {
            return FailDelivery("This version of omp cannot take a message into a running turn.");
        }

        var directive = new SteeringDirective
        {
            Mode = mode == SteeringMode.FollowUp ? SteeringDirectiveMode.FollowUp : SteeringDirectiveMode.Steer,
            Text = text,
        };

        SteeringAck? ack;
        try
        {
            var receipt = await channel.SendAsync(directive, cancellationToken).ConfigureAwait(true);
            ack = await channel.WaitForAckAsync(receipt.Id, AckWait, cancellationToken).ConfigureAwait(true);
        }
        catch (SteeringUnavailableException unavailable)
        {
            return FailDelivery(unavailable.Message);
        }

        if (ack is null)
        {
            return new SteeringDelivery(SteeringDeliveryStatus.SentNotAcknowledged);
        }

        if (ack.Status == SteeringAckStatus.Delivered)
        {
            // The plugin reports whether the session was idle: omp then starts a new turn from the message, and the entry says so.
            if (ack.Idle == true)
            {
                return new SteeringDelivery(SteeringDeliveryStatus.DeliveredStartedNewTurn);
            }

            return new SteeringDelivery(mode == SteeringMode.FollowUp ? SteeringDeliveryStatus.QueuedAfterTurn : SteeringDeliveryStatus.Delivered);
        }

        var verb = ack.Status == SteeringAckStatus.Rejected ? "omp rejected the message" : "omp could not deliver the message";
        return FailDelivery(string.Create(CultureInfo.InvariantCulture, $"{verb} ({AckReasonText(ack.Reason)})"));
    }

    private SteeringDelivery FailDelivery(string reason)
    {
        ReportStatus(reason);
        return SteeringDelivery.Failed(reason);
    }

    private static string AckReasonText(SteeringAckReason? reason) => reason switch
    {
        SteeringAckReason.InvalidJson => "the directive was not valid JSON",
        SteeringAckReason.InvalidSchema => "the directive did not match the schema",
        SteeringAckReason.TooLarge => "the message is too large",
        SteeringAckReason.BadId => "the directive identifier was not valid",
        SteeringAckReason.Duplicate => "the directive was already sent",
        SteeringAckReason.BlankText => "the message was blank",
        SteeringAckReason.DeliverError => "omp refused or failed to take it",
        _ => "no reason was given",
    };

    /// <summary>
    /// Stops the turn in flight through the composed host. The run then ends: messages held behind it are dropped, not sent.
    /// With no turn running the status says so.
    /// </summary>
    /// <returns>A task that completes when the host has accepted the cancellation.</returns>
    private async Task StopTurnAsync()
    {
        if (_options.Prompts is UnconnectedPromptChannel || !Composer.IsStreaming)
        {
            ReportStatus(MainView.NoResponseStatus);
            return;
        }

        // The flag is raised before the cancel, so the turn's end sees it. It is lowered again only when the cancel was not accepted.
        _stopRequested = true;
        var accepted = false;
        try
        {
            await _options.Prompts.CancelAsync(CancellationToken.None).ConfigureAwait(true);
            accepted = true;
        }
        finally
        {
            if (!accepted)
            {
                _stopRequested = false;
            }
        }
    }

    /// <summary>
    /// Sends the draft to the session's engine and streams the turn's events into the transcript. The turn runs in the background, so
    /// the composer clears the draft once the host accepts the send. Without a composed engine the send is refused.
    /// </summary>
    /// <param name="text">The draft text the user sent.</param>
    /// <returns>True when the host accepted the send; false when no engine is composed.</returns>
    private Task<bool> StartTurnAsync(string text)
    {
        if (Terminal is not null && Volatile.Read(ref _disposed) == 0)
        {
            // A terminal has no prompt: the composer only steers a running omp, so a send with omp stopped says why it went nowhere.
            ReportStatus(AgentPanelCopy.TerminalNotRunning);
            return Task.FromResult(false);
        }

        if (_options.Prompts is UnconnectedPromptChannel || Volatile.Read(ref _disposed) != 0)
        {
            return RefuseSendAsync();
        }

        // The last prompt sent is the one a retry sends again. A new send clears the banner of an earlier failure.
        _lastPrompt = text;
        ShowEngineFailure(null);

        // The composer does not send while a response streams. The run stays active until its last turn ends, so a message sent
        // meanwhile steers it or queues behind it.
        Composer.IsStreaming = true;
        Runs.RunStarted();
        var correlationId = Guid.NewGuid().ToString("N");
        EchoUserMessage(text, correlationId);
        TurnChanged?.Invoke(this, new SessionTurnEventArgs(SessionTurnKind.Started));
        var turn = DriveTurnsAsync(text, correlationId);

        // The turn is not awaited. DriveTurnsAsync shows a turn's fault under that turn's own id, so this observes only what escapes it.
        _ = turn.ContinueWith(
            faulted => Dispatcher.UIThread.Post(() => ShowTurnFault(faulted.Exception!.InnerExceptions[0], correlationId)),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Streams one turn's events into the transcript. A fault that ends the turn is not caught here: <see cref="StartTurnAsync"/>
    /// observes it and shows it inline.
    /// </summary>
    /// <param name="text">The prompt text.</param>
    /// <param name="correlationId">The correlation identifier of the turn.</param>
    /// <returns>A task that completes when the turn has ended.</returns>
    private async Task StreamTurnAsync(string text, string correlationId)
    {
        await foreach (var evt in _options.Prompts.SubmitAsync(text, CancellationToken.None).ConfigureAwait(true))
        {
            ShowTurnEvent(evt, correlationId);
        }
    }

    /// <summary>
    /// Runs the turns of one run in send order: the sent prompt first, then each message the run accepted behind it, one turn each.
    /// The run ends when no message is held. A stop or a faulted turn ends it early: whatever is still held is dropped and the
    /// transcript says how many were not sent.
    /// </summary>
    /// <param name="text">The prompt the run starts with.</param>
    /// <param name="correlationId">The correlation identifier of the first turn.</param>
    /// <returns>A task that completes when the run has ended.</returns>
    private async Task DriveTurnsAsync(string text, string correlationId)
    {
        var turnCorrelationId = correlationId;
        string? faultReason = null;
        try
        {
            while (true)
            {
                try
                {
                    await StreamTurnAsync(text, turnCorrelationId).ConfigureAwait(true);
                }
                catch (Exception fault) when (fault is not OperationCanceledException)
                {
                    // The fault ends the run. Its reason is shown under the turn that faulted, and the held messages are dropped in
                    // the finally, so the not-sent line follows the reason.
                    faultReason = fault.Message;
                    ShowTurnFault(fault, turnCorrelationId);
                    ShowEngineFailure($"{MainView.EngineFailedBanner} {fault.Message}");
                    return;
                }

                if (_stopRequested)
                {
                    return;
                }

                // The turn ended on its own. The scheduler dispatches the next follow-up if one waits; the next held message runs next.
                await Runs.RunCompletedAsync(CancellationToken.None).ConfigureAwait(true);
                var next = _queued.TakeNext();
                Runs.NotifySteersHeldChanged();
                if (next is null)
                {
                    return;
                }

                text = next;
                turnCorrelationId = Guid.NewGuid().ToString("N");
                EchoUserMessage(text, turnCorrelationId);
                if (!Runs.IsRunActive)
                {
                    Runs.RunStarted();
                }
            }
        }
        finally
        {
            await FinishRunAsync(turnCorrelationId, faultReason).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Ends a run: drops whatever is still held, completes a run that is still active, says how many messages were not sent, and
    /// raises <see cref="TurnChanged"/>.
    /// </summary>
    /// <param name="correlationId">The correlation identifier of the last turn.</param>
    /// <param name="faultReason">The reason the last turn faulted, or null when the run did not fault.</param>
    /// <returns>A task that completes when the run is settled.</returns>
    private async Task FinishRunAsync(string correlationId, string? faultReason)
    {
        // A run still active here was stopped or its turn faulted. Nothing held is sent after either.
        var notSent = Runs.DiscardQueuedFollowUps() + _queued.Discard();
        Runs.NotifySteersHeldChanged();
        if (Runs.IsRunActive)
        {
            await Runs.RunCompletedAsync(CancellationToken.None).ConfigureAwait(true);
        }

        Composer.IsStreaming = false;
        _stopRequested = false;
        if (notSent > 0)
        {
            ShowNotSent(notSent, correlationId);
        }

        TurnChanged?.Invoke(
            this,
            faultReason is null ? new SessionTurnEventArgs(SessionTurnKind.Completed) : new SessionTurnEventArgs(SessionTurnKind.Faulted, faultReason));
    }

    /// <summary>
    /// Echoes a prompt the composed run has just sent into the transcript as the user's own turn. The echo is a shared
    /// <see cref="UserMessagePayload"/> that the presentation layer folds, so it lands ahead of the reply it prompted.
    /// </summary>
    /// <param name="text">The prompt text that was sent.</param>
    /// <param name="correlationId">The correlation identifier of the turn the prompt starts.</param>
    private void EchoUserMessage(string text, string correlationId)
    {
        Session.ApplyEvent(new StreamEvent
        {
            Seq = _nextSeq,
            SessionId = ViewModel.Id,
            CorrelationId = correlationId,
            Timestamp = _timeProvider.GetUtcNow(),
            Payload = new UserMessagePayload { Text = text },
        });
        _nextSeq++;
    }

    /// <summary>Shows the inline line that says how many held messages were not sent when the run ended.</summary>
    /// <param name="count">The number of messages not sent.</param>
    /// <param name="correlationId">The correlation identifier of the last turn of the run.</param>
    private void ShowNotSent(int count, string correlationId)
    {
        var noun = count == 1 ? "message was" : "messages were";
        var line = new MarkdownBlock { Id = $"not-sent-{correlationId}", Markdown = $"{count} queued {noun} not sent." };
        ShowTurnEvent(new UiBlockEvent(line), correlationId);
    }

    /// <summary>
    /// Shows the fault that ended a turn inline with its reason, as a retryable error so the transcript offers Retry turn (46-UI-SPEC, Turn
    /// error). A cancelled turn is not a fault and adds no row.
    /// </summary>
    /// <param name="fault">The exception that ended the turn.</param>
    /// <param name="correlationId">The correlation identifier of the turn.</param>
    private void ShowTurnFault(Exception fault, string correlationId)
    {
        if (fault is OperationCanceledException)
        {
            return;
        }

        ShowTurnEvent(new ErrorEvent(fault.Message, Retryable: true), correlationId);
    }

    /// <summary>
    /// Shows one host event: a permission request becomes a pending approval, and every event folds into the transcript through the
    /// shared harness projection.
    /// </summary>
    /// <param name="evt">The host event.</param>
    /// <param name="correlationId">The correlation identifier of the turn.</param>
    private void ShowTurnEvent(HarnessEvent evt, string correlationId)
    {
        // The id the host's resolver holds the request under is the one the bridge decides by.
        if (evt is PermissionRequestEvent request)
        {
            RunControls.Approvals.Add(new PendingToolApproval(ViewModel.Id, request.RequestId ?? request.CallId, request.Name, request.PayloadJson));
        }

        foreach (var streamEvent in HarnessEventProjection.ToStreamEvents(_nextSeq, ViewModel.Id, correlationId, _timeProvider.GetUtcNow(), evt))
        {
            Session.ApplyEvent(streamEvent);
            _nextSeq = streamEvent.Seq + 1;
        }
    }

    /// <summary>
    /// Shows the engine-failure banner text, or hides the banner when the text is null. The banner is an alert, so it is announced
    /// when it appears.
    /// </summary>
    /// <param name="text">The banner text, or null to hide the banner.</param>
    private void ShowEngineFailure(string? text)
    {
        _errorText = text;
        FeedbackChanged?.Invoke(this, EventArgs.Empty);
    }
}
