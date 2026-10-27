using System.Net;
using System.Runtime.Versioning;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>
/// The probes answer anonymously from local state and leave no trace: no log line, no telemetry or HTTP client registered. Railway
/// Serverless sleeps a service only after five minutes without outbound traffic, so a probe must not start any.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class WorkerHealthTests
{
    /// <summary>The liveness probe answers 200 without a token.</summary>
    [Fact]
    public async Task HealthzAnswers200Anonymously()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var host = await WorkerHost.StartAsync(tokens, new FixedTimeProvider(), cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = new HttpClient { BaseAddress = host.Address };

        using var response = await client.GetAsync("/healthz", timeout.Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(true)).ShouldBe("ok");
    }

    /// <summary>The readiness probe is 200 when the workspace root and omp exist, and 503 with a fixed reason when omp is missing.</summary>
    [Fact]
    public async Task ReadyzReflectsTheLocalPrerequisites()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var ready = await WorkerHost.StartAsync(tokens, new FixedTimeProvider(), cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var readyScope = ready.ConfigureAwait(true);
        var missing = await WorkerHost.StartAsync(
            tokens,
            new FixedTimeProvider(),
            settings: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Worker:Omp:Path"] = "/nonexistent/omp-does-not-exist" },
            cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var missingScope = missing.ConfigureAwait(true);
        using var readyClient = new HttpClient { BaseAddress = ready.Address };
        using var missingClient = new HttpClient { BaseAddress = missing.Address };

        using var readyResponse = await readyClient.GetAsync("/readyz", timeout.Token).ConfigureAwait(true);
        using var missingResponse = await missingClient.GetAsync("/readyz", timeout.Token).ConfigureAwait(true);

        readyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await missingResponse.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(true)).ShouldBe("{\"status\":\"unready\",\"reason\":\"omp executable not found\"}");
    }

    /// <summary>Probing writes no log line at all, so nothing is emitted while the worker is logically idle.</summary>
    [Fact]
    public async Task ProbesWriteNoLogLines()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var logs = new CapturingLoggerProvider();
        var host = await WorkerHost.StartAsync(tokens, new FixedTimeProvider(), logs: logs, cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var before = logs.Lines.Count;
        using var client = new HttpClient { BaseAddress = host.Address };

        for (var probe = 0; probe < 3; probe++)
        {
            using var health = await client.GetAsync("/healthz", timeout.Token).ConfigureAwait(true);
            using var ready = await client.GetAsync("/readyz", timeout.Token).ConfigureAwait(true);
        }

        logs.Lines.Count.ShouldBe(before);
    }

    /// <summary>The host registers no telemetry exporter and no HTTP client machinery: it has no way to open an outbound connection of its own.</summary>
    [Fact]
    public async Task HostRegistersNoTelemetryAndNoHttpClients()
    {
        using var timeout = TestTimeouts.Create();
        using var tokens = new WorkerTokenFactory();
        var host = await WorkerHost.StartAsync(tokens, new FixedTimeProvider(), cancellationToken: timeout.Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var registered = host.RegisteredServiceTypes;

        registered.ShouldNotContain(name => name.StartsWith("OpenTelemetry", StringComparison.Ordinal));
        registered.ShouldNotContain(name => name.Contains("IHttpClientFactory", StringComparison.Ordinal));
        registered.ShouldNotContain(name => name.StartsWith("Microsoft.Extensions.Http.", StringComparison.Ordinal));
        registered.ShouldNotContain(name => name.StartsWith("Npgsql", StringComparison.Ordinal));
        registered.ShouldNotContain(name => name.StartsWith("StackExchange.Redis", StringComparison.Ordinal));
    }
}
