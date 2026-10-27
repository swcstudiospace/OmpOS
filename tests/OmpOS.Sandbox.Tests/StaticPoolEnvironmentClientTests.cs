using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Tests for <see cref="StaticPoolEnvironmentClient"/>: claiming, releasing, capacity, probing, waking and validation.
/// </summary>
public sealed class StaticPoolEnvironmentClientTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateClaimsTheFirstFreeWorkerAndIsIdempotentPerSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(2);

        var first = await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);
        var again = await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);
        var second = await fixture.Client.CreateContainerAsync("b", ct).ConfigureAwait(true);

        first.ContainerId.ShouldBe("w1");
        first.SessionId.ShouldBe("a");
        first.Phase.ShouldBe(CloudContainerPhase.Running);
        first.BootGeneration.ShouldBe(1);
        first.WorkerBaseUri.ShouldBe(new Uri("http://worker-1.internal:8080/"));
        again.ShouldBe(first);
        second.ContainerId.ShouldBe("w2");
    }

    [Fact]
    public async Task FindReturnsTheClaimOrNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1);

        var before = await fixture.Client.FindContainerAsync("a", ct).ConfigureAwait(true);
        var created = await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);
        var after = await fixture.Client.FindContainerAsync("a", ct).ConfigureAwait(true);
        var other = await fixture.Client.FindContainerAsync("b", ct).ConfigureAwait(true);

        before.ShouldBeNull();
        after.ShouldBe(created);
        other.ShouldBeNull();
    }

    [Fact]
    public async Task ANewSessionGetsCapacityExhaustedWhenEveryWorkerIsClaimedAndReleaseFreesOne()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(2);
        await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);
        await fixture.Client.CreateContainerAsync("b", ct).ConfigureAwait(true);

        var full = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.CreateContainerAsync("c", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var released = await fixture.Client.ReleaseAsync("a", ct).ConfigureAwait(true);
        var releasedAgain = await fixture.Client.ReleaseAsync("a", ct).ConfigureAwait(true);
        var reused = await fixture.Client.CreateContainerAsync("c", ct).ConfigureAwait(true);

        full.Failure.ShouldBe(CloudEnvironmentFailure.CapacityExhausted);
        released.ShouldBeTrue();
        releasedAgain.ShouldBeFalse();
        reused.ContainerId.ShouldBe("w1");
        (await fixture.Client.FindContainerAsync("a", ct).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task ConcurrentClaimsNeverShareAWorker()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(4);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(
            async () =>
            {
                try
                {
                    var container = await fixture.Client.CreateContainerAsync("s" + i, ct).ConfigureAwait(false);
                    return container.ContainerId;
                }
                catch (CloudEnvironmentException ex) when (ex.Failure == CloudEnvironmentFailure.CapacityExhausted)
                {
                    return null;
                }
            },
            ct))).ConfigureAwait(true);

        var claimed = results.Where(id => id is not null).ToList();
        claimed.Count.ShouldBe(4);
        claimed.Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
    }

    [Fact]
    public async Task RequestIdleKeepsTheClaimAndReportsIdleUntilWakeConfirmsTheWorker()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1);
        fixture.Probe.Then(HttpStatusCode.OK);
        var created = await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);

        var idle = await fixture.Client.RequestIdleAsync(created.ContainerId, ct).ConfigureAwait(true);
        var found = await fixture.Client.FindContainerAsync("a", ct).ConfigureAwait(true);
        var woken = await fixture.Client.EnsureAwakeAsync(created.ContainerId, ct).ConfigureAwait(true);
        var foundAfter = await fixture.Client.FindContainerAsync("a", ct).ConfigureAwait(true);

        idle.Phase.ShouldBe(CloudContainerPhase.Idle);
        idle.ContainerId.ShouldBe("w1");
        found!.Phase.ShouldBe(CloudContainerPhase.Idle);
        woken.Phase.ShouldBe(CloudContainerPhase.Running);
        woken.BootGeneration.ShouldBe(1);
        foundAfter!.Phase.ShouldBe(CloudContainerPhase.Running);
    }

    [Fact]
    public async Task EnsureAwakeUsesTheSharedProbeLoopWithRetries()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1);
        fixture.Probe.Then(HttpStatusCode.BadGateway).Then(HttpStatusCode.BadGateway).Then(HttpStatusCode.OK);
        var created = await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);

        var woken = await fixture.Client.EnsureAwakeAsync(created.ContainerId, ct).ConfigureAwait(true);

        woken.Phase.ShouldBe(CloudContainerPhase.Running);
        fixture.Probe.Requests.Count.ShouldBe(3);
        fixture.Probe.Requests.ShouldAllBe(uri => uri == new Uri("http://worker-1.internal:8080/healthz"));
        fixture.Clock.Advanced.ShouldBe(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task EnsureAwakeTimesOutWhenTheWorkerNeverAnswers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1, options => options.WakeTimeout = TimeSpan.FromSeconds(6));
        fixture.Probe.Then(HttpStatusCode.ServiceUnavailable);
        var created = await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.EnsureAwakeAsync(created.ContainerId, ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.WakeTimeout);
        fixture.Probe.Requests.Count.ShouldBe(4);
    }

    [Fact]
    public async Task ProbeIsOneRequestAndTrueOnlyOn2xx()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(2);
        fixture.Probe.Then(HttpStatusCode.OK).Then(HttpStatusCode.BadGateway).ThenConnectionFailure();

        var healthy = await fixture.Client.ProbeAsync("w1", ct).ConfigureAwait(true);
        var badGateway = await fixture.Client.ProbeAsync("w2", ct).ConfigureAwait(true);
        var unreachable = await fixture.Client.ProbeAsync("w1", ct).ConfigureAwait(true);

        healthy.ShouldBeTrue();
        badGateway.ShouldBeFalse();
        unreachable.ShouldBeFalse();
        fixture.Probe.Requests.Count.ShouldBe(3);
        fixture.Probe.Requests[1].ShouldBe(new Uri("http://worker-2.internal:8080/healthz"));
        fixture.Clock.TimersCreated.ShouldBe(0);
    }

    [Fact]
    public async Task UnknownWorkersAreNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1);

        var probe = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.ProbeAsync("missing", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var idle = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.RequestIdleAsync("missing", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var wake = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.EnsureAwakeAsync("missing", ct).ConfigureAwait(true)).ConfigureAwait(true);

        probe.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
        idle.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
        wake.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
    }

    [Fact]
    public async Task ReleasedWorkerCannotBeWokenOrIdledBecauseNoSessionHoldsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1);
        await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);
        await fixture.Client.ReleaseAsync("a", ct).ConfigureAwait(true);

        var idle = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.RequestIdleAsync("w1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        idle.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
    }

    [Fact]
    public async Task VolumeIsBoundToTheClaimAndAttachVerifiesTheMountPath()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1);
        var withoutClaim = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.CreateVolumeAsync("a", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var created = await fixture.Client.CreateContainerAsync("a", ct).ConfigureAwait(true);
        var missingVolume = await fixture.Client.FindVolumeAsync("a", ct).ConfigureAwait(true);

        var volume = await fixture.Client.CreateVolumeAsync("a", ct).ConfigureAwait(true);
        var found = await fixture.Client.FindVolumeAsync("a", ct).ConfigureAwait(true);
        await fixture.Client.AttachVolumeAsync(created.ContainerId, volume.VolumeId, "/data", ct).ConfigureAwait(true);
        var wrongPath = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.AttachVolumeAsync(created.ContainerId, volume.VolumeId, "/elsewhere", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var wrongVolume = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.AttachVolumeAsync(created.ContainerId, "pool-w9", "/data", ct).ConfigureAwait(true)).ConfigureAwait(true);

        withoutClaim.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
        missingVolume.ShouldBeNull();
        volume.ShouldBe(new CloudVolume("pool-w1", "a"));
        found.ShouldBe(volume);
        wrongPath.Failure.ShouldBe(CloudEnvironmentFailure.Unsupported);
        wrongVolume.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
    }

    [Fact]
    public async Task VolumeFilesAreUnsupported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = new PoolFixture(1);

        var append = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.AppendVolumeFileAsync("pool-w1", "t.jsonl", "x", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var read = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await fixture.Client.ReadVolumeFileAsync("pool-w1", "t.jsonl", ct).ConfigureAwait(true)).ConfigureAwait(true);

        append.Failure.ShouldBe(CloudEnvironmentFailure.Unsupported);
        read.Failure.ShouldBe(CloudEnvironmentFailure.Unsupported);
        append.Message.ShouldContain("only reachable inside the worker", Case.Sensitive);
        fixture.Probe.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void InvalidOptionsAreRejected()
    {
        AssertRejected(options => options.Workers = []);
        AssertRejected(options => options.Workers = [Worker("w1", "http://a:1/"), Worker("w1", "http://b:1/")]);
        AssertRejected(options => options.Workers = [Worker("w1", "ftp://a:1/")]);
        AssertRejected(options => options.Workers = [new StaticPoolWorker("w1", new Uri("/relative", UriKind.Relative), "/data")]);
        AssertRejected(options => options.Workers = [new StaticPoolWorker(" ", new Uri("http://a:1/"), "/data")]);
        AssertRejected(options => options.Workers = [new StaticPoolWorker("w1", new Uri("http://a:1/"), " ")]);
        AssertRejected(options => options.WakeTimeout = TimeSpan.Zero);
        AssertRejected(options => options.WakeRetryInterval = TimeSpan.Zero);
        AssertRejected(options => options.HealthPath = " ");
    }

    private static StaticPoolWorker Worker(string id, string uri) => new(id, new Uri(uri), "/data");

    private static void AssertRejected(Action<StaticPoolEnvironmentOptions> mutate)
    {
        var options = new StaticPoolEnvironmentOptions { Workers = [Worker("w1", "http://a:1/")] };
        mutate(options);
        using var http = new HttpClient();

        Should.Throw<ArgumentException>(() => { _ = new StaticPoolEnvironmentClient(http, options, TimeProvider.System); });
    }

    private sealed class PoolFixture : IDisposable
    {
        private readonly HttpClient _client;

        public PoolFixture(int workers, Action<StaticPoolEnvironmentOptions>? configure = null)
        {
            Clock = new AutoAdvanceTimeProvider(Start);
            _client = new HttpClient(Probe, disposeHandler: false);
            var options = new StaticPoolEnvironmentOptions
            {
                Workers = [.. Enumerable.Range(1, workers).Select(i => new StaticPoolWorker(
                    "w" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    new Uri("http://worker-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".internal:8080/"),
                    "/data"))],
            };
            configure?.Invoke(options);
            Client = new StaticPoolEnvironmentClient(_client, options, Clock);
        }

        public ScriptedProbeHandler Probe { get; } = new();

        public AutoAdvanceTimeProvider Clock { get; }

        public StaticPoolEnvironmentClient Client { get; }

        public void Dispose()
        {
            _client.Dispose();
            Probe.Dispose();
        }
    }
}
