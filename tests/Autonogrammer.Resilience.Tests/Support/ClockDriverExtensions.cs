namespace Autonogrammer.Resilience.Tests;

/// <summary>Lets a test finish an operation whose waits are all timers of a <see cref="ManualTimeProvider"/>, without any real time passing.</summary>
internal static class ClockDriverExtensions
{
    private const int MaxSteps = 1_000_000;

    /// <summary>
    /// Fires the earliest armed timer whenever the task is still running, until the task completes. Use it only for operations that never
    /// wait on anything but the clock, because it would otherwise fire a deadline timer early.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="clock">The clock the operation waits on.</param>
    /// <param name="task">The running operation.</param>
    /// <returns>The result of <paramref name="task"/>.</returns>
    public static async Task<T> DriveAsync<T>(this ManualTimeProvider clock, Task<T> task)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(task);
        for (var step = 0; !task.IsCompleted; step++)
        {
            if (step > MaxSteps)
            {
                throw new InvalidOperationException("The operation neither completed nor armed a timer.");
            }

            if (!clock.AdvanceToNextTimer())
            {
                await Task.Yield();
            }
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>Same as <see cref="DriveAsync{T}"/> for an operation without a result.</summary>
    /// <param name="clock">The clock the operation waits on.</param>
    /// <param name="task">The running operation.</param>
    /// <returns>A task that completes with <paramref name="task"/>.</returns>
    public static async Task DriveAsync(this ManualTimeProvider clock, Task task)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(task);
        for (var step = 0; !task.IsCompleted; step++)
        {
            if (step > MaxSteps)
            {
                throw new InvalidOperationException("The operation neither completed nor armed a timer.");
            }

            if (!clock.AdvanceToNextTimer())
            {
                await Task.Yield();
            }
        }

        await task.ConfigureAwait(false);
    }
}
