using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Listens to the event sources of the OpenTelemetry OTLP exporter and SDK at warning level and above. The names were read from the pinned
/// OpenTelemetry.Exporter.OpenTelemetryProtocol 1.20.0 and OpenTelemetry 1.20.0 assemblies (<c>EventSource</c> names
/// <c>OpenTelemetry-Exporter-OpenTelemetryProtocol</c> and <c>OpenTelemetry-Sdk</c>). Only the time of the last warning or error is kept; the event text is never
/// read, because an exporter message can quote the endpoint.
/// </summary>
internal sealed class ExporterEventListener : EventListener
{
    /// <summary>Gets the event source name of the OTLP exporter.</summary>
    public const string ExporterSourceName = "OpenTelemetry-Exporter-OpenTelemetryProtocol";

    /// <summary>Gets the event source name of the OpenTelemetry SDK (batch and metric export processors).</summary>
    public const string SdkSourceName = "OpenTelemetry-Sdk";

    // The base constructor calls OnEventSourceCreated before this class's initializers run, so the only state is a field with a usable default.
    private long _lastFailure;
    private long _count;

    /// <summary>Gets the number of warning or error events seen since the listener started.</summary>
    public long FailureEvents => Interlocked.Read(ref _count);

    /// <summary>Returns whether a warning or error event arrived within <paramref name="window"/>.</summary>
    /// <param name="window">How far back to look.</param>
    /// <returns><see langword="true"/> when the exporter reported a failure recently.</returns>
    public bool FailedWithin(TimeSpan window)
    {
        var last = Interlocked.Read(ref _lastFailure);
        return last != 0 && Stopwatch.GetElapsedTime(last) <= window;
    }

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        ArgumentNullException.ThrowIfNull(eventSource);
        if (eventSource.Name is ExporterSourceName or SdkSourceName)
        {
            EnableEvents(eventSource, EventLevel.Warning);
        }
    }

    /// <inheritdoc />
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.EventSource.Name is not (ExporterSourceName or SdkSourceName) || eventData.Level > EventLevel.Warning)
        {
            return;
        }

        _ = Interlocked.Exchange(ref _lastFailure, Stopwatch.GetTimestamp());
        _ = Interlocked.Increment(ref _count);
    }
}
