namespace OmpOS.Sandbox;

/// <summary>
/// Interface for the sandbox reaper.
/// </summary>
public interface ISandboxReaper
{
    /// <summary>
    /// Reaps expired and orphaned sandboxes once.
    /// </summary>
    /// <param name="now">The current time (for testing with fake clock).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sandboxes reaped.</returns>
    Task<int> ReapOnceAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
