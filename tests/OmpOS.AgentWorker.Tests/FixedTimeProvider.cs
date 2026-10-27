namespace OmpOS.AgentWorker.Tests;

/// <summary>A clock that reports one instant until a test moves it. Timers keep the system behaviour.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    /// <summary>The instant every test starts at.</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now = Start;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => _now += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
