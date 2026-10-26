using System.Collections.ObjectModel;
using System.ComponentModel;
using OmpOS.Presentation.Observable;
using OmpOS.Presentation.Runs;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// The steering input of one session: it sends operator messages through the existing <see cref="RunInputScheduler"/> (steer or follow-up
/// while a run is active), keeps the history of what was sent, and hands each accepted entry to an optional recorder that the head binds
/// to Substrate. Presentation never references the agent-loop library: the recorder is a plain delegate.
/// </summary>
/// <remarks>
/// <para>
/// Order: the message is queued first, the entry is added to the history second, the recorder runs last. A recorder that throws, times out
/// or is cancelled marks the entry <see cref="SteeringRecordStatus.RecordFailed"/> and changes nothing else, so a record failure never loses
/// or duplicates a queued message.
/// </para>
/// <para>
/// Delivery text is what the channel reported and nothing more: a follow-up, or a steer that queued behind one, is
/// <see cref="SteeringDeliveryStatus.QueuedAfterTurn"/>; a steer the input holds is <see cref="SteeringDeliveryStatus.HeldUntilTurnEnds"/>
/// (the held count rose); a steer the input took at once is <see cref="SteeringDeliveryStatus.Delivered"/>. The UI never says that Substrate
/// delivered a message, only that it recorded one.
/// </para>
/// <para>
/// Live delivery: when a head supplies a live delivery delegate (the omp steering channel), a steer or a follow-up goes through it and the
/// scheduler's queue-after-the-turn path is not used. The delegate's <see cref="SteeringDelivery"/> is the delivery status of the entry;
/// a failed delivery adds a <see cref="SteeringDeliveryStatus.Failed"/> entry, is never recorded, and returns
/// <see cref="SteeringSubmitOutcome.Failed"/> so the draft stays in the composer.
/// </para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class SteeringInputModel : ObservableModel, IDisposable
{
    /// <summary>The most characters kept of the text of a history entry.</summary>
    public const int MaxTextChars = 500;

    /// <summary>The most entries kept in the history. The oldest is dropped first.</summary>
    public const int MaxHistory = 100;

    private readonly RunInputScheduler _scheduler;
    private readonly TimeProvider _timeProvider;
    private readonly Func<SteeringEntry, CancellationToken, Task>? _recorder;
    private readonly Func<string, SteeringMode, CancellationToken, Task<SteeringDelivery>>? _liveDelivery;
    private readonly ObservableCollection<SteeringEntry> _history = [];
    private SteeringMode _mode = SteeringMode.Steer;
    private string? _refusalText;
    private bool _substrateReachable = true;
    private int _entryNumber;
    private bool _disposed;

    /// <summary>Creates the steering input of one session.</summary>
    /// <param name="scheduler">The scheduler that routes messages to the run. It stays the owner of the queue and its cap.</param>
    /// <param name="timeProvider">The clock of entry times and of the recorder timeout.</param>
    /// <param name="recorder">
    /// Records an accepted entry, for example as a Substrate note event. It receives the entry, which carries the length and a hash prefix of the
    /// text; it never has to see the text. Null means entries are not recorded.
    /// </param>
    /// <param name="liveDelivery">
    /// Delivers a message into the running session at once, for example through the omp steering channel, and reports what the channel can
    /// prove. Null keeps the queue-after-the-turn behaviour of the scheduler.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="scheduler"/> or <paramref name="timeProvider"/> is null.</exception>
    public SteeringInputModel(
        RunInputScheduler scheduler,
        TimeProvider timeProvider,
        Func<SteeringEntry, CancellationToken, Task>? recorder = null,
        Func<string, SteeringMode, CancellationToken, Task<SteeringDelivery>>? liveDelivery = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _scheduler = scheduler;
        _timeProvider = timeProvider;
        _recorder = recorder;
        _liveDelivery = liveDelivery;
        History = new ReadOnlyObservableCollection<SteeringEntry>(_history);
        _scheduler.PropertyChanged += OnSchedulerChanged;
    }

    /// <summary>Gets how long a recorder may take before its entry is marked <see cref="SteeringRecordStatus.RecordFailed"/>.</summary>
    public static TimeSpan RecorderTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the sent entries, newest first, at most <see cref="MaxHistory"/>.</summary>
    public ReadOnlyObservableCollection<SteeringEntry> History { get; }

    /// <summary>Gets or sets the mode of the next message: a steer or a follow-up.</summary>
    public SteeringMode Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    /// <summary>Gets whether a run is active, so a message is a steer or a follow-up and not a new prompt.</summary>
    public bool IsRunActive => _scheduler.IsRunActive;

    /// <summary>Gets the number of steering messages the engine holds until their turn starts.</summary>
    public int HeldCount => _scheduler.SteersHeld;

    /// <summary>Gets the number of follow-ups waiting for the active run to complete.</summary>
    public int QueuedFollowUpCount => _scheduler.QueuedFollowUpCount;

    /// <summary>Gets the held-steering sentence, or null when none is held.</summary>
    public string? HeldText => AgentPanelCopy.HeldSteering(_scheduler.SteersHeld);

    /// <summary>Gets the text shown when the last message was refused because the queue is full, or null. A later accepted message or the end of the run clears it.</summary>
    public string? RefusalText => _refusalText;

    /// <summary>Gets whether entries can be recorded: a recorder exists and Substrate is not known to be unreachable.</summary>
    public bool IsRecordingAvailable => _recorder is not null && _substrateReachable;

    /// <summary>Gets whether a steer or a follow-up is delivered into the running session at once by a live channel, rather than queued for the end of the turn.</summary>
    public bool IsLive => _liveDelivery is not null;

    /// <summary>
    /// Gets or sets whether Substrate is reachable, as the pipeline reports it. While false, new entries are marked
    /// <see cref="SteeringRecordStatus.NotRecorded"/> and the recorder is not called. It starts true.
    /// </summary>
    public bool IsSubstrateReachable
    {
        get => _substrateReachable;
        set
        {
            if (SetProperty(ref _substrateReachable, value))
            {
                NotifyPropertyChanged(nameof(IsRecordingAvailable));
            }
        }
    }

    /// <summary>Gets the number of history entries Substrate recorded.</summary>
    public int RecordedCount => _history.Count(entry => entry.RecordStatus == SteeringRecordStatus.Recorded);

    /// <summary>Sends a message in the current <see cref="Mode"/>.</summary>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">Cancels the delivery and the record.</param>
    /// <returns>The outcome.</returns>
    public Task<SteeringSubmitOutcome> SubmitAsync(string text, CancellationToken cancellationToken) =>
        SubmitAsync(text, _mode, cancellationToken);

    /// <summary>Sends a message while a run is active.</summary>
    /// <param name="text">The message. White space only is ignored.</param>
    /// <param name="mode">Steer or follow-up.</param>
    /// <param name="cancellationToken">Cancels the delivery and the record.</param>
    /// <returns>The outcome. Only <see cref="SteeringSubmitOutcome.Accepted"/> queued the message and added a history entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined value.</exception>
    public async Task<SteeringSubmitOutcome> SubmitAsync(string text, SteeringMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return SteeringSubmitOutcome.Ignored;
        }

        if (!_scheduler.IsRunActive)
        {
            return SteeringSubmitOutcome.NoActiveRun;
        }

        if (_scheduler.SteersHeld + _scheduler.QueuedFollowUpCount >= RunInputScheduler.MaxQueuedMessages)
        {
            SetProperty(ref _refusalText, AgentPanelCopy.QueueFull(RunInputScheduler.MaxQueuedMessages), nameof(RefusalText));
            return SteeringSubmitOutcome.QueueFull;
        }

        var delivery = _liveDelivery is { } live
            ? await live(text, mode, cancellationToken).ConfigureAwait(true)
            : new SteeringDelivery(await SendAsync(text, mode, cancellationToken).ConfigureAwait(true));
        SetProperty(ref _refusalText, null, nameof(RefusalText));
        var entry = AddEntry(text, mode, delivery);
        if (delivery.IsFailure)
        {
            // Nothing reached the session, so nothing is recorded and the draft stays with the user.
            return SteeringSubmitOutcome.Failed;
        }

        await RecordAsync(entry, cancellationToken).ConfigureAwait(true);
        return SteeringSubmitOutcome.Accepted;
    }

    /// <summary>Re-derives the relative times of the history entries from the clock.</summary>
    public void RefreshTimes()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var entry in _history)
        {
            entry.RefreshTime(now);
        }
    }

    /// <summary>Unsubscribes from the scheduler.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scheduler.PropertyChanged -= OnSchedulerChanged;
    }

    private async Task<SteeringDeliveryStatus> SendAsync(string text, SteeringMode mode, CancellationToken cancellationToken)
    {
        if (mode == SteeringMode.FollowUp)
        {
            _scheduler.QueueFollowUp(text);
            return SteeringDeliveryStatus.QueuedAfterTurn;
        }

        var followUpsWaiting = _scheduler.QueuedFollowUpCount > 0;
        var heldBefore = _scheduler.SteersHeld;
        await _scheduler.SteerAsync(text, cancellationToken).ConfigureAwait(true);
        if (followUpsWaiting)
        {
            return SteeringDeliveryStatus.QueuedAfterTurn;
        }

        return _scheduler.SteersHeld > heldBefore ? SteeringDeliveryStatus.HeldUntilTurnEnds : SteeringDeliveryStatus.Delivered;
    }

    private SteeringEntry AddEntry(string text, SteeringMode mode, SteeringDelivery delivery)
    {
        var now = _timeProvider.GetUtcNow();
        var recordable = IsRecordingAvailable && !delivery.IsFailure;
        _entryNumber++;
        var entry = new SteeringEntry(
            $"steer-{_entryNumber}",
            mode,
            text,
            now,
            delivery.Status,
            recordable ? SteeringRecordStatus.Recording : SteeringRecordStatus.NotRecorded,
            now,
            delivery.Reason);
        _history.Insert(0, entry);
        while (_history.Count > MaxHistory)
        {
            _history.RemoveAt(_history.Count - 1);
        }

        return entry;
    }

    private async Task RecordAsync(SteeringEntry entry, CancellationToken cancellationToken)
    {
        if (entry.RecordStatus != SteeringRecordStatus.Recording || _recorder is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(RecorderTimeout, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            await _recorder(entry, linked.Token).ConfigureAwait(true);
            entry.SetRecordStatus(SteeringRecordStatus.Recorded);
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            // The message is already queued and in the history. A record that fails, times out or is cancelled only changes its own line.
            entry.SetRecordStatus(SteeringRecordStatus.RecordFailed);
        }

        NotifyPropertyChanged(nameof(RecordedCount));
    }

    private void OnSchedulerChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RunInputScheduler.IsRunActive):
                NotifyPropertyChanged(nameof(IsRunActive));
                if (!_scheduler.IsRunActive)
                {
                    SetProperty(ref _refusalText, null, nameof(RefusalText));
                }

                break;
            case nameof(RunInputScheduler.SteersHeld):
                NotifyPropertyChanged(nameof(HeldCount));
                NotifyPropertyChanged(nameof(HeldText));
                break;
            case nameof(RunInputScheduler.QueuedFollowUpCount):
                NotifyPropertyChanged(nameof(QueuedFollowUpCount));
                break;
            default:
                break;
        }
    }
}
