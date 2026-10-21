using OmpOS.Api.Metrics;
using OmpOS.Api.Sse;
using OmpOS.SessionRuns;

namespace OmpOS.Api.Workflows;

/// <summary>
/// The API process's <see cref="ISessionRunObserver"/>: a run started by this process announces each log append through
/// <see cref="SessionChangePublisher"/>, which wakes this process's SSE streams and publishes the change to the other replicas.
/// It also records sandbox provisioning latency.
/// </summary>
internal sealed class ApiSessionRunObserver : ISessionRunObserver
{
    private readonly SessionChangePublisher _publisher;
    private readonly ApiMetrics _metrics;

    /// <summary>Initializes a new instance of the <see cref="ApiSessionRunObserver"/> class.</summary>
    /// <param name="publisher">Announces session changes to this process's streams and to the other replicas.</param>
    /// <param name="metrics">The API metrics.</param>
    public ApiSessionRunObserver(SessionChangePublisher publisher, ApiMetrics metrics)
    {
        _publisher = publisher;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public void SessionChanged(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        // The observer is synchronous and must not block the run. NotifyAsync wakes this process before its first await
        // and never faults, so the discarded task has no unobserved failure.
        _ = _publisher.NotifyAsync(sessionId);
    }

    /// <inheritdoc />
    public void SandboxProvisioned(double elapsedMilliseconds) => _metrics.RecordSandboxProvision(elapsedMilliseconds);
}
