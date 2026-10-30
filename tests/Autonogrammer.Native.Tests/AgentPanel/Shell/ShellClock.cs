namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>A clock the shell tests move by hand. It arms no timer: the shell tests refresh the rail explicitly.</summary>
internal sealed class ShellClock : TimeProvider
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private DateTimeOffset _now = Origin;

    /// <summary>Gets the time the clock reads now.</summary>
    public DateTimeOffset Now => _now;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="by">How far to move it.</param>
    public void Advance(TimeSpan by) => _now += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;
}
