using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Tests.Stages;

/// <summary>
/// Runs a stage source in the background and collects what it emits. <see cref="StepAsync()"/> advances the fake clock by one
/// poll interval and waits for the source to finish that poll and arm its next delay, so every step is deterministic and no
/// test sleeps. A real-time dead-man timeout only turns a hang into a failure.
/// </summary>
internal sealed class StageObserverRun : IAsyncDisposable
{
    private static readonly TimeSpan DeadMan = TimeSpan.FromSeconds(30);

    private readonly StageFakeTimeProvider _clock;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _pump;
    private readonly List<StageEvent> _events = [];
    private readonly Lock _gate = new();

    private StageObserverRun(StageFakeTimeProvider clock, TimeSpan interval, IStageSource source, StageObservationScope scope)
    {
        _clock = clock;
        _interval = interval;
        _pump = Task.Run(() => PumpAsync(source, scope, _cancellation.Token));
    }

    /// <summary>Gets a copy of every event received so far, in order.</summary>
    public IReadOnlyList<StageEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    /// <summary>Gets a task that completes when the observed sequence has ended, or faults when that takes longer than the dead-man timeout.</summary>
    public Task Completion => _pump.WaitAsync(DeadMan);

    /// <summary>Starts observing and waits until the first poll has finished: either a delay is armed or the sequence ended.</summary>
    /// <param name="source">The source to observe.</param>
    /// <param name="scope">The session scope.</param>
    /// <param name="clock">The clock the source was built with.</param>
    /// <param name="interval">The poll interval the source was built with.</param>
    /// <returns>The running observation.</returns>
    public static async Task<StageObserverRun> StartAsync(IStageSource source, StageObservationScope scope, StageFakeTimeProvider clock, TimeSpan interval)
    {
        var run = new StageObserverRun(clock, interval, source, scope);
        await Task.WhenAny(clock.WaitForTimersCreatedAsync(1), run.Completion).ConfigureAwait(true);
        return run;
    }

    /// <summary>Advances the clock by one poll interval and waits for the poll that follows to finish.</summary>
    /// <returns>A task that completes when the source has armed its next delay or ended.</returns>
    public async Task StepAsync()
    {
        var before = _clock.TimersCreated;
        _clock.Advance(_interval);
        await Task.WhenAny(_clock.WaitForTimersCreatedAsync(before + 1), _pump).WaitAsync(DeadMan).ConfigureAwait(true);
    }

    /// <summary>Advances by <paramref name="count"/> poll intervals, one poll each.</summary>
    /// <param name="count">The number of polls.</param>
    /// <returns>A task that completes after the last poll.</returns>
    public async Task StepAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await StepAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Cancels the observation and waits for the sequence to end.</summary>
    /// <returns>A task that completes when the sequence has ended without an exception.</returns>
    public async Task StopAsync()
    {
        await _cancellation.CancelAsync().ConfigureAwait(true);
        await _pump.WaitAsync(DeadMan).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync().ConfigureAwait(true);
        await _pump.WaitAsync(DeadMan).ConfigureAwait(true);
        _cancellation.Dispose();
    }

    private async Task PumpAsync(IStageSource source, StageObservationScope scope, CancellationToken cancellationToken)
    {
        await foreach (var stageEvent in source.ObserveAsync(scope, cancellationToken).ConfigureAwait(false))
        {
            Add(stageEvent);
        }
    }

    private void Add(StageEvent stageEvent)
    {
        lock (_gate)
        {
            _events.Add(stageEvent);
        }
    }
}
