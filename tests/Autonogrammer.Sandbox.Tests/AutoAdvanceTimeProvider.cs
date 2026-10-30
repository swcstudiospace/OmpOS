namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// A clock whose timers fire at once: <see cref="CreateTimer"/> runs the callback synchronously and advances the clock by the
/// due time, so <c>Task.Delay(interval, provider, token)</c> completes immediately and the elapsed time stays deterministic.
/// </summary>
internal sealed class AutoAdvanceTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoAdvanceTimeProvider"/> class.
    /// </summary>
    /// <param name="utcNow">The initial time.</param>
    public AutoAdvanceTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    /// <summary>Gets how many timers (delays) were created.</summary>
    public int TimersCreated { get; private set; }

    /// <summary>Gets the time elapsed since the clock was created, summed over the timers' due times.</summary>
    public TimeSpan Advanced { get; private set; }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        TimersCreated++;
        if (dueTime > TimeSpan.Zero)
        {
            _utcNow = _utcNow.Add(dueTime);
            Advanced += dueTime;
        }

        callback(state);
        return new CompletedTimer();
    }

    private sealed class CompletedTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
