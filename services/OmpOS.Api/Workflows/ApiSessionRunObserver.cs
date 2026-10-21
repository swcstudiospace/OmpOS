using OmpOS.Api.Metrics;
using OmpOS.Api.Sse;
using OmpOS.SessionRuns;

namespace OmpOS.Api.Workflows;

/// <summary>
/// The API process's <see cref="ISessionRunObserver"/>: wakes this process's SSE streams after a run appends to the log and
/// records sandbox provisioning latency. A routed run executes in the worker, which observes nothing; its appends reach this
/// process's streams through the cross-process poll in <see cref="SessionEventStreamer"/>.
/// </summary>
internal sealed class ApiSessionRunObserver : ISessionRunObserver
{
    private readonly SessionEventStreamer _streamer;
    private readonly ApiMetrics _metrics;

    /// <summary>Initializes a new instance of the <see cref="ApiSessionRunObserver"/> class.</summary>
    /// <param name="streamer">The SSE broadcaster for this process.</param>
    /// <param name="metrics">The API metrics.</param>
    public ApiSessionRunObserver(SessionEventStreamer streamer, ApiMetrics metrics)
    {
        _streamer = streamer;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public void SessionChanged(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        _streamer.Notify(sessionId);
    }

    /// <inheritdoc />
    public void SandboxProvisioned(double elapsedMilliseconds) => _metrics.RecordSandboxProvision(elapsedMilliseconds);
}
