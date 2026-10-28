namespace Autonogrammer.SessionRuns;

/// <summary>
/// An <see cref="ISessionRunObserver"/> that ignores every signal. Used by hosts that have no event streams or metrics
/// to feed, such as a background worker.
/// </summary>
public sealed class NullSessionRunObserver : ISessionRunObserver
{
    /// <inheritdoc />
    public void SessionChanged(string sessionId)
    {
    }

    /// <inheritdoc />
    public void SandboxProvisioned(double elapsedMilliseconds)
    {
    }
}
