using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Runs;

/// <summary>
/// Routes user messages sent while a run is active (ADE-03), through a deterministic queue. A steering message goes to
/// <see cref="IRunInput"/> at once. A follow-up waits in send order and is dispatched only after <see cref="RunCompletedAsync"/>
/// reports that the active run completed. Each dispatched follow-up starts a new run, so the next follow-up waits for that
/// run's completion.
/// </summary>
/// <remarks>
/// <see cref="Pending"/> is <see cref="PendingRunInput.FollowUp"/> while any follow-up is queued. Otherwise it is
/// <see cref="PendingRunInput.Steering"/> while a steering delivery is in flight, and <see cref="PendingRunInput.None"/> when
/// neither applies. A failed follow-up dispatch leaves it at the head of the queue and no run active. The scheduler is not
/// thread-safe; it runs on the UI thread that owns the presentation models.
/// </remarks>
public sealed class RunInputScheduler : ObservableModel
{
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

    /// <summary>Delivers a steering message to the active run immediately.</summary>
    /// <param name="text">The steering message.</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task that completes when the input has accepted the steering message.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty or white space.</exception>
    /// <exception cref="InvalidOperationException">No run is active.</exception>
    public async Task SteerAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!_runActive)
        {
            throw new InvalidOperationException("No run is active. Send the message as a new prompt.");
        }

        _steeringInFlight++;
        UpdatePending();
        try
        {
            await _input.SteerAsync(text, cancellationToken).ConfigureAwait(true);
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
    /// <exception cref="InvalidOperationException">No run is active.</exception>
    public void QueueFollowUp(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!_runActive && !_dispatching)
        {
            throw new InvalidOperationException("No run is active. Send the message as a new prompt.");
        }

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

    private void UpdatePending() =>
        SetProperty(ref _pending, ComputePending(), nameof(Pending));

    private PendingRunInput ComputePending() =>
        _followUps.Count > 0 ? PendingRunInput.FollowUp
        : _steeringInFlight > 0 ? PendingRunInput.Steering
        : PendingRunInput.None;
}
