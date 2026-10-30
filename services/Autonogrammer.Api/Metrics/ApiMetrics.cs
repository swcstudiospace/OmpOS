using System.Diagnostics.Metrics;
using Autonogrammer.Api.Telemetry;

namespace Autonogrammer.Api.Metrics;

/// <summary>
/// The API host's metric instruments. The sandbox instrument below is emitted by the
/// session-run activities (Phase 3), so dashboards can reference it.
/// </summary>
internal sealed class ApiMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _sessionsCreated;
    private readonly Histogram<double> _streamLatencyMs;
    private readonly Counter<double> _parserFallbackRate;
    private readonly Histogram<double> _sandboxProvisionMs;

    /// <summary>Initializes a new instance of the <see cref="ApiMetrics"/> class.</summary>
    /// <param name="meterFactory">The meter factory.</param>
    public ApiMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(ApiTelemetry.SourceName);
        _sessionsCreated = _meter.CreateCounter<long>("autonogrammer.sessions.count", description: "Sessions created through POST /sessions.");
        _streamLatencyMs = _meter.CreateHistogram<double>("autonogrammer.stream.latency", unit: "ms", description: "Event append-to-delivery latency on the SSE stream.");
        _parserFallbackRate = _meter.CreateCounter<double>("autonogrammer.parser.fallback.rate", description: "Parser fallback recognizer hits, one per prompt that no other recognizer claims.");
        _sandboxProvisionMs = _meter.CreateHistogram<double>("autonogrammer.sandbox.provision.time", unit: "ms", description: "Sandbox provisioning duration (emitted from Phase 3).");
    }

    /// <summary>Records one created session.</summary>
    public void SessionCreated() => _sessionsCreated.Add(1);

    /// <summary>Records an SSE event delivery latency in milliseconds.</summary>
    /// <param name="milliseconds">The append-to-delivery latency.</param>
    public void RecordStreamLatency(double milliseconds) => _streamLatencyMs.Record(milliseconds);

    /// <summary>Records one prompt that fell through to the parser fallback recognizer.</summary>
    public void RecordParserFallback() => _parserFallbackRate.Add(1);

    /// <summary>Records a sandbox provisioning duration in milliseconds.</summary>
    /// <param name="milliseconds">The provision duration.</param>
    public void RecordSandboxProvision(double milliseconds) => _sandboxProvisionMs.Record(milliseconds);

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();
}
