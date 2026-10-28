namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Observes how a task ends without rethrowing its exception. A plugin's failure is then recorded as a quarantine reason
/// instead of crashing the host.
/// </summary>
internal static class TaskFault
{
    /// <summary>
    /// Waits for the task to complete in any state and returns the exception it failed with.
    /// </summary>
    /// <param name="task">A task that was started on the host's behalf.</param>
    /// <returns>
    /// The base exception of a faulted task, an <see cref="OperationCanceledException"/> for a canceled task, or null when
    /// the task completed normally.
    /// </returns>
    public static async Task<Exception?> AwaitAsync(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
        await task.ContinueWith(
            static _ => { },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default).ConfigureAwait(false);

        if (task.IsCanceled)
        {
            return new OperationCanceledException();
        }

        return task.Exception?.GetBaseException();
    }
}
