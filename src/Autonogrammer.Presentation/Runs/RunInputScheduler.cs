using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Runs;

/// <summary>
/// Routes user messages sent while a run is active (ADE-03), through a deterministic queue. A steering message goes to
/// <see cref="IRunInput"/> at once, unless a follow-up already waits: then it queues behind that follow-up, so the messages keep
/// their send order. A follow-up waits in send order and is dispatched only after <see cref="RunCompletedAsync"/> reports that the
/// active run completed. Each dispatched follow-up starts a new run, so the next follow-up waits for that run's completion.
/// </summary>
/// <remarks>
/// <see cref="Pending"/> is <see cref="PendingRunInput.FollowUp"/> while any follow-up is queued. Otherwise it is
/// <see cref="PendingRunInput.Steering"/> while a steering delivery is in flight, and <see cref="PendingRunInput.None"/> when
/// neither applies. A failed follow-up dispatch leaves it at the head of the queue and no run active. The scheduler is not
/// thread-safe; it runs on the UI thread that owns the presentation models.
/// </remarks>
public sealed class RunInputScheduler : ObservableModel
{
    /// <summary>
    /// The most user messages that may wait to start: the steers the input has accepted that have not started their turn, plus the
    /// follow-ups queued behind the active run. A send past this count is refused with <see cref="InvalidOperationException"/>.
    /// </summary>
    public const int MaxQueuedMessages = 16;

    private readonly IRunInput _input;
    private readonly Queue<string> _followUps = new();
    private bool _runActive;
    private bool _dispatching;
    private int _steeringInFlight;
    private PendingRunInput _pending = PendingRunInput.None;

    /// <summary>Creates a scheduler that routes messages to <paramref name="input"/>.</summary>
    /// <param name="input">The input channel of the run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public RunInputScheduler(IRunInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        _input = input;
    }

    /// <summary>Gets whether a run is active. A dispatched follow-up makes its run active once the input accepts it.</summary>
    public bool IsRunActive => _runActive;

    /// <summary>Gets the number of follow-ups waiting in send order.</summary>
    public int QueuedFollowUpCount => _followUps.Count;

    /// <summary>Gets the kind of user input that is waiting on the run.</summary>
    public PendingRunInput Pending => _pending;

    /// <summary>
    /// Gets the number of steering messages the input has accepted that have not yet started their turn. The engine holds each such
    /// message until its own turn starts, so the count spans every turn of the run and a steer leaves it only then.
    /// </summary>
    public int SteersHeld => _input.SteersHeld;

    /// <summary>Records that a run started from a prompt the user sent.</summary>
    /// <exception cref="InvalidOperationException">A run is already active or a follow-up is being dispatched.</exception>
    public void RunStarted()
    {
        if (_runActive || _dispatching)
        {
            throw new InvalidOperationException("A run is already active.");
        }

        SetProperty(ref _runActive, true, nameof(IsRunActive));
    }

    /// <summary>
    /// Hands a steering message to the active run. The input receives it at once; an engine without a steer command holds it until the
    /// active turn ends. While follow-ups wait, the steer queues behind them as a follow-up instead, so the messages keep their send order.
    /// </summary>
    /// <param name="text">The steering message.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task that completes when the input has accepted the steering message, or when it has been queued behind a follow-up.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty or white space.</exception>
    /// <exception cref="InvalidOperationException">No run is active, or the run already holds <see cref="MaxQueuedMessages"/> messages.</exception>
    public async Task SteerAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!_runActive)
        {
            throw new InvalidOperationException("No run is active. Send the message as a new prompt.");
        }

        if (_followUps.Count > 0)
        {
            QueueFollowUp(text);
            return;
        }

        EnsureRoomForAnotherMessage();
        _steeringInFlight++;
        UpdatePending();
        try
        {
            await _input.SteerAsync(text, cancellationToken).ConfigureAwait(true);
            NotifySteersHeldChanged();
        }
        finally
        {
            _steeringInFlight--;
            UpdatePending();
        }
    }

    /// <summary>Queues a follow-up. It is dispatched after the active run completes.</summary>
    /// <param name="text">The follow-up message.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty or white space.</exception>
    /// <exception cref="InvalidOperationException">No run is active, or the run already holds <see cref="MaxQueuedMessages"/> messages.</exception>
    public void QueueFollowUp(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!_runActive && !_dispatching)
        {
            throw new InvalidOperationException("No run is active. Send the message as a new prompt.");
        }

        EnsureRoomForAnotherMessage();
        _followUps.Enqueue(text);
        NotifyPropertyChanged(nameof(QueuedFollowUpCount));
        UpdatePending();
    }

    /// <summary>
    /// Records that the active run completed, then dispatches the next queued follow-up if one waits. The follow-up leaves the
    /// queue only after the input accepts it, so a failed dispatch loses nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>A task that completes when the run's completion has been handled and any follow-up has been dispatched.</returns>
    /// <exception cref="InvalidOperationException">No run is active.</exception>
    public async Task RunCompletedAsync(CancellationToken cancellationToken)
    {
        if (!_runActive)
        {
            throw new InvalidOperationException("No run is active, so there is no completion to record.");
        }

        SetProperty(ref _runActive, false, nameof(IsRunActive));
        if (_followUps.Count == 0)
        {
            return;
        }

        _dispatching = true;
        var accepted = false;
        try
        {
            await _input.FollowUpAsync(_followUps.Peek(), cancellationToken).ConfigureAwait(true);
            accepted = true;
        }
        finally
        {
            _dispatching = false;
            if (accepted)
            {
                _followUps.Dequeue();
                SetProperty(ref _runActive, true, nameof(IsRunActive));
                NotifyPropertyChanged(nameof(QueuedFollowUpCount));
            }

            UpdatePending();
        }
    }

    /// <summary>
    /// Drops every follow-up still waiting in the queue, so none is dispatched. The owner of the run calls it when the user stops the run.
    /// </summary>
    /// <returns>The number of follow-ups dropped.</returns>
    public int DiscardQueuedFollowUps()
    {
        var count = _followUps.Count;
        _followUps.Clear();
        NotifyPropertyChanged(nameof(QueuedFollowUpCount));
        UpdatePending();
        return count;
    }

    /// <summary>
    /// Raises a change for <see cref="SteersHeld"/>. The owner of the run calls it after it takes or discards held messages, since
    /// those change the input's count outside the scheduler.
    /// </summary>
    public void NotifySteersHeldChanged() => NotifyPropertyChanged(nameof(SteersHeld));

    private void EnsureRoomForAnotherMessage()
    {
        if (_input.SteersHeld + _followUps.Count >= MaxQueuedMessages)
        {
            throw new InvalidOperationException(
                $"The run already holds {MaxQueuedMessages} queued messages. Wait for the active run to finish before queuing more.");
        }
    }

    private void UpdatePending() =>
        SetProperty(ref _pending, ComputePending(), nameof(Pending));

    private PendingRunInput ComputePending() =>
        _followUps.Count > 0 ? PendingRunInput.FollowUp
        : _steeringInFlight > 0 ? PendingRunInput.Steering
        : PendingRunInput.None;
}
