using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>The cost counters: awake seconds are added when a stretch ends, and refusals by a cap are counted by reason.</summary>
public sealed class CloudMetricsTests
{
    /// <summary>When an agent goes idle its awake seconds are added to the compute counter, once.</summary>
    [Fact]
    public async Task ComputeSeconds_AreCountedWhenAnAwakeStretchEnds()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var listener = new Listener(host.Services.GetRequiredService<IMeterFactory>());
        _ = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);

        host.Clock.Advance(TimeSpan.FromMinutes(16));
        _ = await host.SweepAsync(ct).ConfigureAwait(true);
        _ = await host.SweepAsync(ct).ConfigureAwait(true);

        listener.Total("autonogrammer.cloud.compute.seconds").ShouldBe(960, 1.0);
        listener.Total("autonogrammer.cloud.agents.idle").ShouldBe(1);
        listener.Total("autonogrammer.cloud.agents.created").ShouldBe(1);
    }

    /// <summary>A refusal by the agent cap is counted with its reason.</summary>
    [Fact]
    public async Task CapRefusals_AreCountedByReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Limits:MaxAgentsPerOwner"] = "1", ["Cloud:Limits:MaxConcurrentRunningPerOwner"] = "1" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var listener = new Listener(host.Services.GetRequiredService<IMeterFactory>());
        _ = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        using var client = host.Client();

        using var refused = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", CloudApiHost.DefaultCreateBody(), ct).ConfigureAwait(true);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        listener.Total("autonogrammer.cloud.agents.limit_refused").ShouldBe(1);
        listener.Tags("autonogrammer.cloud.agents.limit_refused").ShouldContain("reason=agents", StringComparer.Ordinal);
    }

    private sealed class Listener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string Name, double Value, string Tags)> _measurements = new();

        public Listener(IMeterFactory scope)
        {
            // Only the meters this host's factory made: other hosts in the same test run publish the same instrument names.
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter.Scope, scope) && instrument.Name.StartsWith("autonogrammer.cloud.", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument.Name, value, tags));
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument.Name, value, tags));
            _listener.Start();
        }

        public double Total(string name) => _measurements.Where(measurement => string.Equals(measurement.Name, name, StringComparison.Ordinal)).Sum(static measurement => measurement.Value);

        public IReadOnlyList<string> Tags(string name) => [.. _measurements.Where(measurement => string.Equals(measurement.Name, name, StringComparison.Ordinal)).Select(static measurement => measurement.Tags)];

        public void Dispose() => _listener.Dispose();

        private void Record(string name, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var text = string.Join(',', tags.ToArray().Select(static tag => tag.Key + "=" + tag.Value));
            _measurements.Enqueue((name, value, text));
        }
    }
}
