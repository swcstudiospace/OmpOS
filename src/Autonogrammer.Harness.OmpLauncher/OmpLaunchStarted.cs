namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The process started. The result owns its session: disposing the result disposes the session, which kills the process
/// tree if it is still running.
/// </summary>
public sealed class OmpLaunchStarted : OmpLaunchResult, IDisposable
{
    private readonly OmpSession _session;

    /// <summary>Takes ownership of a running session.</summary>
    /// <param name="session">The running session. The result disposes it.</param>
    public OmpLaunchStarted(OmpSession session)
    {
        _session = session;
    }

    /// <summary>The running session.</summary>
    public OmpSession Session => _session;

    /// <summary>Disposes the session.</summary>
    public void Dispose() => _session.Dispose();
}
