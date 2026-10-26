using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using OmpOS.Contracts.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// OPS-03 proof: the <c>ompos.*</c> instruments record values (not just exist) and the
/// vertical-slice endpoints emit spans from the OmpOS activity source.
/// </summary>
public sealed class OperationsTelemetryTests
{
    /// <summary>Verifies creating a session records at least one <c>ompos.sessions.count</c> hit.</summary>
    [Fact]
    public async Task SessionsCount_IncrementsOnSessionCreated()
    {
        var measurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (string.Equals(instrument.Name, "ompos.sessions.count", StringComparison.Ordinal))
            {
                observer.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) => measurements.Add(measurement));
        listener.Start();

        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        _ = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        measurements.Sum().ShouldBeGreaterThanOrEqualTo(1L, "creating a session must record one sessions.count hit");
    }

    /// <summary>Verifies draining a session event stream records an <c>ompos.stream.latency</c> value.</summary>
    [Fact]
    public async Task StreamLatency_RecordsAfterStreamCompletes()
    {
        var measurements = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (string.Equals(instrument.Name, "ompos.stream.latency", StringComparison.Ordinal))
            {
                observer.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, state) => measurements.Add(measurement));
        listener.Start();

        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"trace me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var events = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken).ConfigureAwait(true);
        events.ShouldNotBeEmpty("the stream must terminate so the delivery latency is recorded");

        measurements.ShouldNotBeEmpty("completing a session event stream must record a stream latency value");
    }

    /// <summary>Verifies session creation and prompt submission each emit an OmpOS endpoint span.</summary>
    [Fact]
    public async Task Spans_SessionCreationAndPromptSubmission_Emitted()
    {
        var seen = new ConcurrentBag<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => string.Equals(source.Name, "OmpOS.Api", StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => seen.Add(activity.OperationName),
        };
        ActivitySource.AddActivityListener(listener);

        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"trace me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        seen.Contains("POST /sessions", StringComparer.Ordinal).ShouldBeTrue("session creation must emit an OmpOS endpoint span");
        seen.Contains("POST /sessions/{id}/prompts", StringComparer.Ordinal).ShouldBeTrue("prompt submission must emit an OmpOS endpoint span");
    }

    /// <summary>Verifies a prompt run records <c>ompos.sandbox.provision.time</c> values, in milliseconds, once its sandbox is provisioned.</summary>
    [Fact]
    public async Task SandboxProvisionTime_RecordsAfterPromptRunProvisionsSandbox()
    {
        var measurements = new ConcurrentBag<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (string.Equals(instrument.Name, "ompos.sandbox.provision.time", StringComparison.Ordinal))
            {
                observer.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, tags, state) => measurements.Add(measurement));
        listener.Start();

        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"provision me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);

        measurements.ShouldNotBeEmpty("a prompt run provisions its sandbox, which records the provision time");
        measurements.ShouldAllBe(static value => value >= 0d);
    }
}
