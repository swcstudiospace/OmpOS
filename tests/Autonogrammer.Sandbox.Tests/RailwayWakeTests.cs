using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Tests for idle and wake handling of <see cref="RailwayCloudEnvironmentClient"/>: Railway sleeps implicitly, so idle is a
/// report and wake is a readiness probe with retries.
/// </summary>
public sealed class RailwayWakeTests
{
    private const string HealthUri = "http://autonogrammer-agent-s1.railway.internal:8080/healthz";

    [Fact]
    public async Task WakeRetriesA502UntilTheWorkerAnswers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        RegisterService(harness, "SUCCESS");
        harness.Probe.Then(HttpStatusCode.BadGateway).Then(HttpStatusCode.BadGateway).Then(HttpStatusCode.OK);

        var container = await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true);

        harness.Probe.Requests.Count.ShouldBe(3);
        harness.Probe.Requests.ShouldAllBe(uri => uri == new Uri(HealthUri));
        harness.Clock.TimersCreated.ShouldBe(2);
        harness.Clock.Advanced.ShouldBe(TimeSpan.FromSeconds(4));
        container.Phase.ShouldBe(CloudContainerPhase.Running);
        container.ContainerId.ShouldBe("svc-1");
        container.BootGeneration.ShouldBe(1);
        container.WorkerBaseUri.ShouldBe(new Uri("http://autonogrammer-agent-s1.railway.internal:8080/"));
    }

    [Fact]
    public async Task WakeTimesOutAfterTheSixtySecondWindowMeasuredOnTheClock()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        RegisterService(harness, "SLEEPING");
        harness.Probe.Then(HttpStatusCode.BadGateway);

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.WakeTimeout);
        harness.Clock.Advanced.ShouldBe(TimeSpan.FromSeconds(60));
        harness.Clock.TimersCreated.ShouldBe(30);
        harness.Probe.Requests.Count.ShouldBe(31);
    }

    [Fact]
    public async Task WakeWindowAndRetryIntervalFollowTheOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness(options =>
        {
            options.WakeTimeout = TimeSpan.FromSeconds(10);
            options.WakeRetryInterval = TimeSpan.FromSeconds(5);
        });
        RegisterService(harness, "SUCCESS");
        harness.Probe.Then(HttpStatusCode.ServiceUnavailable);

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.WakeTimeout);
        harness.Probe.Requests.Count.ShouldBe(3);
        harness.Clock.Advanced.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ConnectionFailuresAndPerRequestTimeoutsAreRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        RegisterService(harness, "SUCCESS");
        harness.Probe.ThenConnectionFailure().ThenTimeout().Then(HttpStatusCode.GatewayTimeout).Then(HttpStatusCode.NoContent);

        var container = await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true);

        container.Phase.ShouldBe(CloudContainerPhase.Running);
        harness.Probe.Requests.Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task OtherStatusesFailAtOnceWithoutRetry(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        RegisterService(harness, "SUCCESS");
        harness.Probe.Then(status);

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Transport);
        failure.Message.ShouldContain(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), Case.Sensitive);
        harness.Probe.Requests.Count.ShouldBe(1);
        harness.Clock.TimersCreated.ShouldBe(0);
    }

    [Fact]
    public async Task BootGenerationIncrementsOnlyWhenTheContainerWasReportedSleeping()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SLEEPING"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SLEEPING"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));
        harness.Probe.Then(HttpStatusCode.OK);

        var found = await harness.Client.FindContainerAsync("s1", ct).ConfigureAwait(true);
        var woken = await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true);
        var again = await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true);

        found!.Phase.ShouldBe(CloudContainerPhase.Sleeping);
        found.BootGeneration.ShouldBe(1);
        woken.Phase.ShouldBe(CloudContainerPhase.Running);
        woken.BootGeneration.ShouldBe(2);
        again.BootGeneration.ShouldBe(2);
    }

    [Fact]
    public async Task ASleepThatHappenedAfterTheLastReportIsStillCountedOnWake()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SLEEPING"));
        harness.Probe.Then(HttpStatusCode.OK);

        var found = await harness.Client.FindContainerAsync("s1", ct).ConfigureAwait(true);
        var woken = await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true);

        found!.Phase.ShouldBe(CloudContainerPhase.Running);
        woken.BootGeneration.ShouldBe(2);
    }

    [Fact]
    public async Task RequestIdleReportsIdleWhenRailwayHasNotSleptItAndNeverClaimsOtherwise()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SLEEPING"));

        var idle = await harness.Client.RequestIdleAsync("svc-1", ct).ConfigureAwait(true);
        var slept = await harness.Client.RequestIdleAsync("svc-1", ct).ConfigureAwait(true);

        idle.Phase.ShouldBe(CloudContainerPhase.Idle);
        slept.Phase.ShouldBe(CloudContainerPhase.Sleeping);
        harness.Probe.Requests.ShouldBeEmpty();
        harness.Api.Requests.Select(request => request.Query).ShouldAllBe(query => query.StartsWith("query ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IdleThenWakeWithoutARailwaySleepKeepsTheBootGeneration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));
        harness.Probe.Then(HttpStatusCode.OK);

        var idle = await harness.Client.RequestIdleAsync("svc-1", ct).ConfigureAwait(true);
        var woken = await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true);

        idle.Phase.ShouldBe(CloudContainerPhase.Idle);
        woken.Phase.ShouldBe(CloudContainerPhase.Running);
        woken.BootGeneration.ShouldBe(1);
    }

    [Fact]
    public async Task WakeOfAnUnknownServiceIsNotFoundAndProbesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services());

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.EnsureAwakeAsync("svc-gone", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
        harness.Probe.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task CloudSessionHostCanDriveARailwayContainerThroughIdleAndWake()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SLEEPING"));
        harness.Api.Data("projectVolumes", RailwayHarness.Volumes(("vol-1", RailwayHarness.Label)));
        harness.Probe.Then(HttpStatusCode.BadGateway).Then(HttpStatusCode.OK);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));
        using var host = new CloudSessionHost(harness.Client, clock, new CloudSessionHostOptions { IdleWindow = TimeSpan.FromHours(1), VolumeMountPath = "/data" });

        var opened = await host.OpenAsync("s1", ct).ConfigureAwait(true);
        clock.Advance(TimeSpan.FromHours(1));
        var idled = await host.SleepIdleAsync(ct).ConfigureAwait(true);

        opened.ContainerId.ShouldBe("svc-1");
        idled.Count.ShouldBe(1);
        host.Get("s1")!.State.ShouldBe(CloudSessionState.Sleeping);

        // Host.ReattachAsync would also read the transcript, which the Railway client cannot do (no file endpoint),
        // so the wake is driven on the client directly after the host marked the session Sleeping.
        var woken = await harness.Client.EnsureAwakeAsync("svc-1", ct).ConfigureAwait(true);
        woken.BootGeneration.ShouldBe(2);
        harness.Probe.Requests.Count.ShouldBe(2);
    }

    private static void RegisterService(RailwayHarness harness, string status)
    {
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments(status));
    }
}
