namespace Autonogrammer.SessionRuns;

/// <summary>
/// Receives the signals a session run emits that the hosting process may act on: a session's event log or status
/// changed, and a sandbox was provisioned.
/// </summary>
/// <remarks>
/// The API implements this over its SSE streams and metrics, so a run that appends from the API wakes the readers of
/// that process. A process with no streams or metrics to feed, such as a background worker, uses
/// <see cref="NullSessionRunObserver"/>. Observers are called on the run's own task and must not block.
/// </remarks>
public interface ISessionRunObserver
{
    /// <summary>Notifies that a session's event log or status changed.</summary>
    /// <param name="sessionId">The session identifier.</param>
    void SessionChanged(string sessionId);

    /// <summary>Records one sandbox provision.</summary>
    /// <param name="elapsedMilliseconds">How long the provision took, in milliseconds.</param>
    void SandboxProvisioned(double elapsedMilliseconds);
}
