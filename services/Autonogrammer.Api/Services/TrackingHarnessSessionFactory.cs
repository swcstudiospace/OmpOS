using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Wraps the registered <see cref="IHarnessSessionFactory"/> and tells <see cref="LiveSessionTracker"/> about each engine session a run creates.
/// It returns the inner factory's session untouched, so every capability interface the engine session implements stays reachable.
/// </summary>
internal sealed class TrackingHarnessSessionFactory : IHarnessSessionFactory
{
    private readonly IHarnessSessionFactory _inner;
    private readonly LiveSessionTracker _tracker;

    /// <summary>Initializes a new instance of the <see cref="TrackingHarnessSessionFactory"/> class.</summary>
    /// <param name="inner">The factory that creates the engine sessions.</param>
    /// <param name="tracker">The tracker that follows them.</param>
    public TrackingHarnessSessionFactory(IHarnessSessionFactory inner, LiveSessionTracker tracker)
    {
        _inner = inner;
        _tracker = tracker;
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt) => _inner.CreateSession(prompt);

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt, string sessionId, string runId)
    {
        var session = _inner.CreateSession(prompt, sessionId, runId);
        _tracker.Track(sessionId, session);
        return session;
    }
}
