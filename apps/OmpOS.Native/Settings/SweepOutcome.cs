namespace OmpOS.Native.Settings;

/// <summary>What one pass of <see cref="SessionLifecycleSweeper"/> did.</summary>
/// <param name="Slept">The sessions whose engine was stopped because they sat idle for the idle stop window.</param>
/// <param name="Removed">The completed sessions that were removed because they outlived the retention window.</param>
internal sealed record SweepOutcome(int Slept, int Removed)
{
    /// <summary>Gets the outcome of a pass that changed nothing.</summary>
    public static SweepOutcome None { get; } = new(Slept: 0, Removed: 0);
}
