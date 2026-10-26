namespace OmpOS.SessionRuns.Tests;

/// <summary>Observer that records every signal a run emits.</summary>
internal sealed class RecordingObserver : ISessionRunObserver
{
    /// <summary>Gets the session identifiers reported as changed, in order.</summary>
    public List<string> SessionChanges { get; } = [];

    /// <summary>Gets the provision durations reported, in order.</summary>
    public List<double> ProvisionMilliseconds { get; } = [];

    /// <inheritdoc />
    public void SessionChanged(string sessionId) => SessionChanges.Add(sessionId);

    /// <inheritdoc />
    public void SandboxProvisioned(double elapsedMilliseconds) => ProvisionMilliseconds.Add(elapsedMilliseconds);
}
