namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>Drives a <see cref="ManualTimeProvider"/> while a task waits on it.</summary>
internal static class TimeDriver
{
    /// <summary>Advances the clock in steps until a condition holds.</summary>
    /// <param name="time">The clock.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="step">How far to move per step.</param>
    /// <returns>A task that completes when it holds; it fails after the clock moved 200 steps.</returns>
    public static async Task AdvanceUntilAsync(this ManualTimeProvider time, Func<bool> condition, TimeSpan step)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(condition);
        for (int i = 0; i < 200 && !condition(); i++)
        {
            time.Advance(step);
            await Task.Delay(5).ConfigureAwait(false);
        }

        if (!condition())
        {
            throw new TimeoutException("The condition did not hold after the clock advanced.");
        }
    }

    /// <summary>Waits, in real time, until a condition holds.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns>A task that completes when it holds; it fails after about ten seconds.</returns>
    public static async Task UntilAsync(Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        for (int i = 0; i < 2000 && !condition(); i++)
        {
            await Task.Delay(5).ConfigureAwait(false);
        }

        if (!condition())
        {
            throw new TimeoutException("The condition did not hold in time.");
        }
    }
}
