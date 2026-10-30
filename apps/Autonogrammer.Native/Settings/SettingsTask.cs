using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Settings;

/// <summary>Turns the synchronous read or write of a head settings source into the task its page awaits, so an expected failure reaches the page as a faulted task and not as a throw at the call.</summary>
internal static class SettingsTask
{
    /// <summary>Runs a read.</summary>
    /// <typeparam name="T">The type read.</typeparam>
    /// <param name="read">The read.</param>
    /// <returns>A completed task with the value, or a task faulted with the <see cref="SettingsSourceException"/> the read threw.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="read"/> is null.</exception>
    public static Task<T> ReadAsync<T>(Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        try
        {
            return Task.FromResult(read());
        }
        catch (SettingsSourceException exception)
        {
            return Task.FromException<T>(exception);
        }
    }

    /// <summary>Runs a write.</summary>
    /// <param name="write">The write.</param>
    /// <returns>A completed task, or a task faulted with the <see cref="SettingsSourceException"/> the write threw.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="write"/> is null.</exception>
    public static Task WriteAsync(Action write)
    {
        ArgumentNullException.ThrowIfNull(write);
        try
        {
            write();
            return Task.CompletedTask;
        }
        catch (SettingsSourceException exception)
        {
            return Task.FromException(exception);
        }
    }
}
