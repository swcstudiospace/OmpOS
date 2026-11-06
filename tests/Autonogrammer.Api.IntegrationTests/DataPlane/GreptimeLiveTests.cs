using System.Net;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// Live proof against a real GreptimeDB: a host exports to the <c>autonogrammer</c> database and the metric table of the dependency-state gauge appears there.
/// It runs only when <c>AUTONOGRAMMER_LIVE_GREPTIME=1</c> and <c>AUTONOGRAMMER_TEST_GREPTIME_URL</c> is set, which
/// <c>bash scripts/test-services.sh up --with-greptime</c> does (D-54).
/// </summary>
[Collection(ProcessPathSerialization.Name)]
public sealed class GreptimeLiveTests
{
    private const string Headers = "X-Greptime-DB-Name=autonogrammer,X-Greptime-Pipeline-Name=greptime_trace_v1,X-Greptime-Trace-Table-Name=autonogrammer_traces";

    /// <summary>Verifies the exported dependency-state gauge lands in the autonogrammer database.</summary>
    [Fact]
    [Trait("Category", "Live")]
    public async Task Metrics_LandInTheAutonogrammerDatabase()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AUTONOGRAMMER_LIVE_GREPTIME"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_GREPTIME=1 after scripts/test-services.sh up --with-greptime to run the GreptimeDB live check.");
        }

        var baseUrl = Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_GREPTIME_URL");
        baseUrl.ShouldNotBeNullOrWhiteSpace("scripts/test-services.sh up --with-greptime exports AUTONOGRAMMER_TEST_GREPTIME_URL");
        var testToken = TestContext.Current.CancellationToken;
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        using var createBody = new FormUrlEncodedContent([new("sql", "CREATE DATABASE IF NOT EXISTS autonogrammer")]);
        using var create = await http.PostAsync(new Uri("v1/sql", UriKind.Relative), createBody, testToken).ConfigureAwait(true);
        create.StatusCode.ShouldBe(HttpStatusCode.OK);

        var names = new[] { "Otel__Exporter", "OTEL_EXPORTER_OTLP_ENDPOINT", "OTEL_EXPORTER_OTLP_PROTOCOL", "OTEL_EXPORTER_OTLP_HEADERS", "OTEL_METRIC_EXPORT_INTERVAL" };
        var previous = names.ToDictionary(static name => name, static name => Environment.GetEnvironmentVariable(name), StringComparer.Ordinal);
        try
        {
            Environment.SetEnvironmentVariable("Otel__Exporter", "otlp");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", baseUrl.TrimEnd('/') + "/v1/otlp");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", Headers);
            Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");
            using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
            _ = await DragonflyResilience.Create().Pipeline.ExecuteAsync(OperationClass.Interactive, true, static _ => Task.FromResult(1), testToken).ConfigureAwait(true);

            var found = false;
            for (var attempt = 0; attempt < 60 && !found; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), testToken).ConfigureAwait(true);
                using var showBody = new FormUrlEncodedContent([new("sql", "SHOW TABLES")]);
                using var tables = await http.PostAsync(new Uri("v1/sql?db=autonogrammer", UriKind.Relative), showBody, testToken).ConfigureAwait(true);
                found = (await tables.Content.ReadAsStringAsync(testToken).ConfigureAwait(true)).Contains("autonogrammer_dataplane_dependency_state", StringComparison.Ordinal);
            }

            found.ShouldBeTrue("the gauge table exists in the autonogrammer database");
        }
        finally
        {
            foreach (var (name, value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
