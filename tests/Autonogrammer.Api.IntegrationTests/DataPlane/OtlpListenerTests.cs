using System.Net;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// Telemetry cutover to GreptimeDB, observed on the wire: the OTLP exporter sends the routing headers of the <c>autonogrammer</c> database and the
/// <c>autonogrammer.*</c> instruments of the <c>Autonogrammer.Api</c> meter and the resilience meter; and the exporter monitor sees a failing export. The exporter
/// is selected the way a deployment does it, through the process environment, which is restored in every case (D-63).
/// </summary>
/// <remarks>
/// The process environment is shared by every host of the test run. Hosts that other classes build while it is set also export to the collector of the running
/// test, which carries the same headers; the assertions only look for the presence of what a host of this test sends.
/// </remarks>
[Collection(nameof(OtlpListenerTests))]
public sealed class OtlpListenerTests
{
    private const string GreptimeHeaders = "X-Greptime-DB-Name=autonogrammer,X-Greptime-Pipeline-Name=greptime_trace_v1,X-Greptime-Trace-Table-Name=autonogrammer_traces";
    private static readonly TimeSpan ExportWait = TimeSpan.FromSeconds(45);

    /// <summary>Verifies exports carry the Greptime database header and the metric batch holds the new instruments under the API meter.</summary>
    [Fact]
    public async Task Exporter_SendsTheGreptimeHeaders_AndTheAutonogrammerInstruments()
    {
        var testToken = TestContext.Current.CancellationToken;
        var collector = await RecordingOtlpCollector.StartAsync(testToken).ConfigureAwait(true);
        await using var collectorScope = collector.ConfigureAwait(true);

        using var environment = new OtlpEnvironment(collector.Endpoint);
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The resilience instruments record on the static meter; one successful call gives the exporter a measurement of each pipeline instrument.
        var pipeline = DragonflyResilience.Create(new DragonflyResilienceOptions()).Pipeline;
        (await pipeline.ExecuteAsync(OperationClass.Interactive, true, static _ => Task.FromResult(1), testToken).ConfigureAwait(true)).ShouldBe(1);

        var metrics = await collector.WaitForAsync(
            static export => export.Path.EndsWith("/v1/metrics", StringComparison.Ordinal)
                && export.BodyContains("autonogrammer.dataplane.dependency.state")
                && export.BodyContains("autonogrammer.dataplane.call.duration"),
            ExportWait,
            testToken).ConfigureAwait(true);
        var traces = await collector.WaitForAsync(static export => export.Path.EndsWith("/v1/traces", StringComparison.Ordinal), ExportWait, testToken).ConfigureAwait(true);

        collector.Exports().Length.ShouldBeGreaterThan(0, "the listener saw at least one export request");
        metrics.ShouldBeTrue("a metric batch with the dependency-state gauge and the call-duration histogram arrived");
        traces.ShouldBeTrue("a trace batch arrived");
        var metricBatch = collector.Exports().First(static export => export.Path.EndsWith("/v1/metrics", StringComparison.Ordinal) && export.BodyContains("autonogrammer.dataplane.dependency.state"));
        metricBatch.BodyContains("Autonogrammer.Api").ShouldBeTrue("the gauge belongs to the Autonogrammer.Api meter");
        metricBatch.BodyContains("autonogrammer-api").ShouldBeTrue("the resource names the service");
        foreach (var export in collector.Exports().Where(static export => export.Path.StartsWith("/v1/", StringComparison.Ordinal)))
        {
            export.Headers["X-Greptime-DB-Name"].ShouldBe("autonogrammer");
            export.Headers["X-Greptime-Pipeline-Name"].ShouldBe("greptime_trace_v1");
            export.Headers["X-Greptime-Trace-Table-Name"].ShouldBe("autonogrammer_traces");
        }
    }

    /// <summary>Verifies the monitor sees the failing export: the event listener counts events and the status of the exporter turns failing.</summary>
    [Fact]
    public async Task Monitor_SeesAFailingExport_ThroughTheEventListener()
    {
        var testToken = TestContext.Current.CancellationToken;
        var collector = await RecordingOtlpCollector.StartAsync(testToken).ConfigureAwait(true);
        await using var collectorScope = collector.ConfigureAwait(true);
        collector.ExportStatus = (int)HttpStatusCode.InternalServerError;

        using var environment = new OtlpEnvironment(collector.Endpoint);
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        var monitor = factory.Services.GetRequiredService<TelemetryExportMonitor>();

        var failing = await collector.WaitForAsync(static export => export.Path.StartsWith("/v1/", StringComparison.Ordinal), ExportWait, testToken).ConfigureAwait(true);
        failing.ShouldBeTrue("the exporter sent a request that the collector refused");
        var observed = SpinWait.SpinUntil(() => monitor.FailureEvents > 0, TimeSpan.FromSeconds(20));

        observed.ShouldBeTrue("a wrong event source name would leave the listener with zero events");
        monitor.IsFailing.ShouldBeTrue();
        using var status = await client.GetAsync(new Uri("/status/data-plane", UriKind.Relative), testToken).ConfigureAwait(true);
        (await status.Content.ReadAsStringAsync(testToken).ConfigureAwait(true)).ShouldContain("ExporterFailing");
    }

    private sealed class OtlpEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = [];

        public OtlpEnvironment(Uri endpoint)
        {
            Set("Otel__Exporter", "otlp");
            Set("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint.ToString().TrimEnd('/'));
            Set("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            Set("OTEL_EXPORTER_OTLP_HEADERS", GreptimeHeaders);
            Set("OTEL_METRIC_EXPORT_INTERVAL", "500");
            Set("OTEL_BSP_SCHEDULE_DELAY", "500");
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        private void Set(string name, string value)
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
