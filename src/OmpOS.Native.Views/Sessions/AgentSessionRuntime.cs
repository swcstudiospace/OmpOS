using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Environments;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.Attachments;
using OmpOS.Native.Views.RunControls;
using OmpOS.Native.Views.Terminal;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Composer.Attachments;
using OmpOS.Presentation.Mentions;
using OmpOS.Presentation.Modes;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.SideChats;
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
    private readonly SlashPaletteBinding? _slashBinding;
    private readonly MentionComposerModel? _mentions;
    private readonly ImageInputBinding? _imageBinding;
    private IReadOnlyList<PromptResource> _lastResources = [];
    private IReadOnlyList<PromptImage> _lastImages = [];
    private string? _lastRequestId;
    private bool _turnInterrupted;

    private ModelPickerBinding? BindModels(AgentSessionRuntimeOptions options)
    {
        if (options.Models is not { } models || options.TerminalStarter is not null)
        {
            return null;
        }

        // A terminal runs its own omp, which owns its model: only a chat session offers the model chip.
        var binding = new ModelPickerBinding(models, options.ConnectModels);
        Composer.ModelPicker = binding.Picker;
        return binding;
    }

    private ImageInputBinding? BindImages(AgentSessionRuntimeOptions options)
    {
        // A terminal runs its own omp, and a session with no engine has nothing to send an image to: only a connected chat session offers image attachments. The model of the
        // attachments follows what the engine said in initialize and, when a lookup knows it, whether the selected model reads images.
        if (options.TerminalStarter is not null || options.Prompts is UnconnectedPromptChannel)
        {
            return null;
        }

        var images = new ComposerAttachmentsModel(new AvaloniaImageThumbnailer());
        Composer.Images = images;
        Composer.ImageSender = StartTurnWithImagesAsync;
        return new ImageInputBinding(images, options.Models, options.ModelVision);
    }

    private void DisposeBindings()
    {
        _modelBinding?.Dispose();
        _imageBinding?.Dispose();
        _modeBinding?.Dispose();
        _thinkingBinding?.Dispose();
        _slashBinding?.Dispose();
    }

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
        _modelBinding = BindModels(options);

        if (options.SessionOptions is { } sessionOptions && options.TerminalStarter is null)
        {
            // A terminal runs its own omp, which owns its mode and thinking level: only a chat session offers the chips.
            _modeBinding = new OptionChipBinding(sessionOptions, "mode", "Mode", chip => Composer.ModeChip = chip);
            _thinkingBinding = new OptionChipBinding(sessionOptions, "thinking", "Thinking", chip => Composer.ThinkingChip = chip);
        }

        if (options.SessionCommands is { } sessionCommands && options.TerminalStarter is null)
        {
            // A terminal runs its own omp, which shows its own commands in its own terminal: only a chat session offers the slash palette.
            var host = new DesktopSlashActionHost(ComposerSlashTargets.Create(Composer, options.SlashTargets));
            _slashBinding = new SlashPaletteBinding(sessionCommands, SlashCommandCatalogue.Shipped, options.ConnectModels, host, options.SlashDiscovery, _timeProvider);
            Composer.SlashPalette = _slashBinding.Palette;
            _ = _slashBinding.RefreshDiscoveryAsync(CancellationToken.None);
        }

        if (options.MentionSources is { } mentionSources && options.TerminalStarter is null)
        {
            // A terminal runs its own omp and has no composer prompt: only a chat session offers @ mentions and the context ring.
            _mentions = new MentionComposerModel(mentionSources(), timeProvider: _timeProvider);
            Composer.Mentions = _mentions;
            Composer.ContextRing = new ContextRingModel();
            Composer.MentionSender = StartTurnWithMentionsAsync;
        }

        _imageBinding = BindImages(options);

        if (options.LiveEvents is { } liveEvents)
        {
            liveEvents.HookNoticeRaised += OnHookNoticeRaised;
            liveEvents.ConnectionChanged += OnConnectionChanged;
            liveEvents.TurnInterrupted += OnTurnInterrupted;
        }

        ViewModel = CreateViewModel(options);
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

    private AgentSessionViewModel CreateViewModel(AgentSessionRuntimeOptions options)
    {
        return new AgentSessionViewModel(new AgentSessionOptions
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
    }

    /// <summary>Occurs on the UI context when a run started, completed or faulted.</summary>
    public event EventHandler<SessionTurnEventArgs>? TurnChanged;

    /// <summary>Occurs on the UI context when a hook did something the thread must show: a denial, a missing consent, added context or a continuation.</summary>
    public event EventHandler<HookNoticeRaisedEventArgs>? HookNoticeRaised;

    /// <summary>Occurs on the UI context when the connection of a remote session changed.</summary>
    public event EventHandler<SessionConnectionChangedEventArgs>? ConnectionChanged;

    /// <summary>Occurs on the UI context when a turn ended because the connection dropped. The conversation is safe on the engine's side and the next prompt resumes it.</summary>
    public event EventHandler<TurnInterruptedEventArgs>? TurnInterrupted;

    /// <summary>
    /// Occurs on the UI context when the connection status of the remote environment the session runs on changed: reconnecting, unreachable, or a turn interrupted by the loss of the
    /// connection. A null status means the connection is healthy and the rail shows the session's own status again. A local session never raises it.
    /// </summary>
    public event EventHandler<RemoteEnvironmentStatusChangedEventArgs>? RemoteStatusChanged;

    /// <summary>Occurs on the UI context when the engine reported how much of the context window the conversation uses. The composer's context ring is already updated.</summary>
    public event EventHandler? ContextUsageChanged;

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

    /// <summary>Gets the identifier of the session in the head's registry.</summary>
    public string SessionId => ViewModel.Id;

    /// <summary>Gets the correlation identifier of the last turn this runtime started, or null before the first one. A copy command offers it as the request identifier.</summary>
    public string? LastRequestId => _lastRequestId;

    /// <summary>Gets the function that returns the file system of the ssh host the session runs on, or null for a local session.</summary>
    public Func<CancellationToken, Task<OmpOS.Presentation.Editor.IAsyncWorkspaceFileSystem>>? RemoteFiles => _options.RemoteFiles;

    /// <summary>Returns the conversation as plain text (the user's messages and the assistant's answers), for an @ mention of this chat.</summary>
    /// <returns>The text, or an empty string before the first message.</returns>
    internal string TranscriptText()
    {
        var builder = new System.Text.StringBuilder();
        foreach (var block in Session.Tree.Blocks)
        {
            switch (block)
            {
                case UserMessageBlock user:
                    builder.Append("User: ").Append(user.Text).Append("\n\n");
                    break;
                case MarkdownBlock markdown:
                    builder.Append("Assistant: ").Append(markdown.Markdown).Append("\n\n");
                    break;
                default:
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Gets the forker of the session, which creates a new session that continues from the same history, or null when the session cannot be forked (a terminal, or an omp that does not advertise it).</summary>
    public ISessionForker? Forker => _options.Forker;

    /// <summary>Gets the engine of the session's side chats, or null when the session offers none.</summary>
    public ISideChatEngine? SideChatEngine => _options.SideChatEngine;

    /// <summary>
    /// Gets or sets the preamble of the active custom mode. While it is active, each prompt sent to the engine (a typed turn and a message held behind a run) is sent through
    /// <see cref="IPromptPreamble.Apply"/>; the transcript echoes what the user typed, and text that starts with <c>/</c> is not changed, because omp runs a leading slash command from the start of the prompt.
    /// </summary>
    public IPromptPreamble? PromptPreamble { get; set; }

    /// <summary>Gets the absolute path of the workspace the session runs in. The Changes bar reads the agent's edits against it.</summary>
    internal string WorkspaceRoot => _options.Scope.WorkspaceRoot;

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
        DisposeBindings();
        if (_options.LiveEvents is { } liveEvents)
        {
            liveEvents.HookNoticeRaised -= OnHookNoticeRaised;
            liveEvents.ConnectionChanged -= OnConnectionChanged;
            liveEvents.TurnInterrupted -= OnTurnInterrupted;
        }

        _mentions?.Dispose();
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

        _ = StartTurnCoreAsync(prompt, _lastResources, _lastImages);
    }

    /// <summary>
    /// Gives the session the parts of the head that native slash commands drive (Settings, a new chat, a new terminal, a side chat). The view that shows the session calls it once the session is shown, because
    /// those parts belong to the shell and not to the session. The palette lists the commands that now have a target as enabled. It does nothing for a terminal session, which has no slash palette.
    /// </summary>
    /// <param name="targets">The head's targets. The runtime adds the targets its own composer reaches.</param>
    /// <exception cref="ArgumentNullException"><paramref name="targets"/> is null.</exception>
    public void AttachSlashTargets(DesktopSlashTargets targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        _slashBinding?.SetActionHost(new DesktopSlashActionHost(ComposerSlashTargets.Create(Composer, targets)));
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
        else if (!await StartTurnAsync(text, cancellationToken).ConfigureAwait(true))
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
    private Task<bool> StartTurnAsync(string text) => StartTurnCoreAsync(text, []);

    /// <summary>
    /// Sends text to the session's engine as the user's turn, exactly as the composer does: a slash command is resolved first, the echo and the stream follow. The shell uses it for a
    /// message that does not come from the composer (a queued message, a hand-off).
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <param name="cancellationToken">Cancels the call before anything is sent.</param>
    /// <returns>True when the host accepted the send; false when no engine is composed or the session cannot take it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public Task<bool> StartTurnAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        return StartTurnCoreAsync(text, []);
    }

    /// <summary>
    /// Sends the draft with the content of its attached @-mentions: each mention travels as an embedded resource next to the text, in attachment order.
    /// </summary>
    /// <param name="text">The draft text the user sent.</param>
    /// <param name="attachments">The attached mentions and the content read for each.</param>
    /// <returns>True when the host accepted the send; false when no engine is composed.</returns>
    private Task<bool> StartTurnWithMentionsAsync(string text, IReadOnlyList<MentionAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        return StartTurnCoreAsync(
            text,
            [.. attachments.Select(static attachment => new PromptResource(
                attachment.Mention.Location,
                attachment.Mention.MimeType,
                attachment.Content.Text,
                attachment.Mention.Display,
                attachment.Content.SizeBytes,
                attachment.Content.Truncated))]);
    }

    /// <summary>
    /// Sends the draft with its attached images, and with the content of its attached @-mentions when it has any: the mentions travel as embedded resources and the images as image blocks, in attachment order.
    /// The text may be blank when an image is attached.
    /// </summary>
    /// <param name="text">The draft text the user sent.</param>
    /// <param name="attachments">The attached mentions and the content read for each; possibly none.</param>
    /// <param name="images">The attached images.</param>
    /// <returns>True when the host accepted the send; false when no engine is composed.</returns>
    private Task<bool> StartTurnWithImagesAsync(string text, IReadOnlyList<MentionAttachment> attachments, IReadOnlyList<ComposerAttachment> images)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(images);
        return StartTurnCoreAsync(
            text,
            [.. attachments.Select(static attachment => new PromptResource(
                attachment.Mention.Location,
                attachment.Mention.MimeType,
                attachment.Content.Text,
                attachment.Mention.Display,
                attachment.Content.SizeBytes,
                attachment.Content.Truncated))],
            [.. images.Select(static image => new PromptImage(image.Name, image.MimeType, image.Data))]);
    }

    /// <summary>Returns what the transcript shows for a message that carries images: the words (possibly none) and the names of the images, because the transcript shows text only.</summary>
    private static string DescribeImageTurn(string text, IReadOnlyList<PromptImage> images)
    {
        var attached = (images.Count == 1 ? "Attached image: " : "Attached images: ") + string.Join(", ", images.Select(static image => image.Name));
        return string.IsNullOrWhiteSpace(text) ? attached : text + "\n\n" + attached;
    }

    private async Task<bool> StartTurnCoreAsync(string text, IReadOnlyList<PromptResource> resources, IReadOnlyList<PromptImage>? images = null)
    {
        if (Terminal is not null && Volatile.Read(ref _disposed) == 0)
        {
            // A terminal has no prompt: the composer only steers a running omp, so a send with omp stopped says why it went nowhere.
            ReportStatus(AgentPanelCopy.TerminalNotRunning);
            return false;
        }

        if (_options.Prompts is UnconnectedPromptChannel || Volatile.Read(ref _disposed) != 0)
        {
            return await RefuseSendAsync().ConfigureAwait(true);
        }

        // A slash command is resolved first: a native action runs in the head and sends nothing, a discovered command, skill or subagent becomes the prompt, and any other text is sent as typed.
        if (_slashBinding is not null && text.Length > 0 && text[0] is '/' or '&')
        {
            var dispatch = await _slashBinding.DispatchAsync(text, CancellationToken.None).ConfigureAwait(true);
            if (dispatch.Kind == SlashDispatchKind.Handled)
            {
                ReportStatus(dispatch.Text);
                return true;
            }

            text = dispatch.Text;
        }

        // The last prompt sent is the one a retry sends again. A new send clears the banner of an earlier failure.
        images ??= [];
        _lastPrompt = text;
        _lastResources = resources;
        _lastImages = images;
        ShowEngineFailure(null);

        // The composer does not send while a response streams. The run stays active until its last turn ends, so a message sent
        // meanwhile steers it or queues behind it.
        Composer.IsStreaming = true;
        Runs.RunStarted();
        var correlationId = Guid.NewGuid().ToString("N");
        _lastRequestId = correlationId;
        EchoUserMessage(images.Count == 0 ? text : DescribeImageTurn(text, images), correlationId);
        TurnChanged?.Invoke(this, new SessionTurnEventArgs(SessionTurnKind.Started));
        var turn = DriveTurnsAsync(text, resources, images, correlationId);

        // The turn is not awaited. DriveTurnsAsync shows a turn's fault under that turn's own id, so this observes only what escapes it.
        _ = turn.ContinueWith(
            faulted => Dispatcher.UIThread.Post(() => ShowTurnFault(faulted.Exception!.InnerExceptions[0], correlationId)),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        return true;
    }

    /// <summary>
    /// Streams one turn's events into the transcript. A fault that ends the turn is not caught here: <see cref="StartTurnCoreAsync"/>
    /// observes it and shows it inline. The prompt goes through the active custom mode's preamble, when there is one; the transcript keeps what the user typed.
    /// </summary>
    /// <param name="text">The prompt text.</param>
    /// <param name="resources">The mentions that travel with the prompt.</param>
    /// <param name="images">The images that travel with the prompt.</param>
    /// <param name="correlationId">The correlation identifier of the turn.</param>
    /// <returns>A task that completes when the turn has ended.</returns>
    private async Task StreamTurnAsync(string text, IReadOnlyList<PromptResource> resources, IReadOnlyList<PromptImage> images, string correlationId)
    {
        var prompt = PromptPreamble is { IsActive: true } preamble && !text.StartsWith('/') ? preamble.Apply(text) : text;
        var promptOptions = new PromptOptions { Resources = resources, Images = images };
        await foreach (var evt in _options.Prompts.SubmitAsync(prompt, promptOptions, CancellationToken.None).ConfigureAwait(true))
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
    /// <param name="resources">The mentions that travel with the first prompt.</param>
    /// <param name="images">The images that travel with the first prompt.</param>
    /// <param name="correlationId">The correlation identifier of the first turn.</param>
    /// <returns>A task that completes when the run has ended.</returns>
    private async Task DriveTurnsAsync(string text, IReadOnlyList<PromptResource> resources, IReadOnlyList<PromptImage> images, string correlationId)
    {
        var turnCorrelationId = correlationId;
        string? faultReason = null;
        try
        {
            while (true)
            {
                try
                {
                    await StreamTurnAsync(text, resources, images, turnCorrelationId).ConfigureAwait(true);
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
                resources = [];
                images = [];
                turnCorrelationId = Guid.NewGuid().ToString("N");
                _lastRequestId = turnCorrelationId;
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
        // Usage is session state, not transcript content: it feeds the context ring and shows nothing in the thread.
        if (evt is ContextUsageEvent usage)
        {
            Composer.ContextRing?.ReportUsage(usage.Used, usage.Size > 0 ? usage.Size : null);
            ContextUsageChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

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

    private void OnHookNoticeRaised(object? sender, HookNoticeRaisedEventArgs e) => RaiseOnUi(() => HookNoticeRaised?.Invoke(this, e));

    private void OnConnectionChanged(object? sender, SessionConnectionChangedEventArgs e) =>
        RaiseOnUi(() =>
        {
            ConnectionChanged?.Invoke(this, e);
            var healthy = e.State == RemoteConnectionState.Connected;
            if (healthy)
            {
                _turnInterrupted = false;
            }

            RemoteStatusChanged?.Invoke(this, new RemoteEnvironmentStatusChangedEventArgs(healthy ? null : RemoteStatusDescriber.FromConnection(e.State, _turnInterrupted)));
        });

    private void OnTurnInterrupted(object? sender, TurnInterruptedEventArgs e) =>
        RaiseOnUi(() =>
        {
            _turnInterrupted = true;
            TurnInterrupted?.Invoke(this, e);
            RemoteStatusChanged?.Invoke(this, new RemoteEnvironmentStatusChangedEventArgs(RemoteStatusDescriber.FromConnection(RemoteConnectionState.Disconnected, turnInterrupted: true)));
        });

    /// <summary>Runs a raise on the UI context that owns the presentation models; a signal that arrives after the runtime was disposed is dropped.</summary>
    /// <param name="raise">The raise.</param>
    private void RaiseOnUi(Action raise)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            raise();
        }
        else
        {
            Dispatcher.UIThread.Post(raise);
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
