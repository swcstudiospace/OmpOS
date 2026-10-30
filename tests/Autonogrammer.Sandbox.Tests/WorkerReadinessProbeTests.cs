using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Tests for <see cref="WorkerReadinessProbe"/>: the single health request and the wake retry loop.
/// </summary>
public sealed class WorkerReadinessProbeTests
{
    private static readonly Uri Worker = new("http://worker-1.internal:8080/");
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProbeOnceIsTrueOnlyForA2xxAnswerFromTheHealthPath()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedProbeHandler().Then(HttpStatusCode.OK).Then(HttpStatusCode.ServiceUnavailable).Then(HttpStatusCode.NotFound).ThenConnectionFailure().ThenTimeout();
        using var client = new HttpClient(handler, disposeHandler: false);
        var probe = CreateProbe(client, new AutoAdvanceTimeProvider(Start));

        var results = new List<bool>();
        for (var i = 0; i < 5; i++)
        {
            results.Add(await probe.ProbeOnceAsync(Worker, ct).ConfigureAwait(true));
        }

        bool[] expected = [true, false, false, false, false];
        results.ToArray().ShouldBe(expected);
        handler.Requests.ShouldAllBe(uri => uri == new Uri("http://worker-1.internal:8080/healthz"));
    }

    [Fact]
    public async Task WaitReturnsAtOnceWhenTheWorkerIsAlreadyReady()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedProbeHandler().Then(HttpStatusCode.OK);
        using var client = new HttpClient(handler, disposeHandler: false);
        var clock = new AutoAdvanceTimeProvider(Start);

        await CreateProbe(client, clock).WaitUntilReadyAsync(Worker, ct).ConfigureAwait(true);

        handler.Requests.Count.ShouldBe(1);
        clock.TimersCreated.ShouldBe(0);
    }

    [Fact]
    public async Task WaitRetriesTheSignsOfAWakingWorkerEveryIntervalUntilItAnswers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedProbeHandler()
            .Then(HttpStatusCode.BadGateway)
            .Then(HttpStatusCode.BadGateway)
            .Then(HttpStatusCode.ServiceUnavailable)
            .ThenConnectionFailure()
            .Then(HttpStatusCode.OK);
        using var client = new HttpClient(handler, disposeHandler: false);
        var clock = new AutoAdvanceTimeProvider(Start);

        await CreateProbe(client, clock).WaitUntilReadyAsync(Worker, ct).ConfigureAwait(true);

        handler.Requests.Count.ShouldBe(5);
        clock.TimersCreated.ShouldBe(4);
        clock.Advanced.ShouldBe(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task WaitThrowsWakeTimeoutOnceTheWindowHasElapsed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedProbeHandler().Then(HttpStatusCode.BadGateway);
        using var client = new HttpClient(handler, disposeHandler: false);
        var clock = new AutoAdvanceTimeProvider(Start);

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await CreateProbe(client, clock).WaitUntilReadyAsync(Worker, ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.WakeTimeout);
        clock.Advanced.ShouldBe(TimeSpan.FromSeconds(60));
        handler.Requests.Count.ShouldBe(31);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task WaitFailsAtOnceOnAStatusThatIsNotAWakeSign(HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedProbeHandler().Then(status);
        using var client = new HttpClient(handler, disposeHandler: false);
        var clock = new AutoAdvanceTimeProvider(Start);

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await CreateProbe(client, clock).WaitUntilReadyAsync(Worker, ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.Transport);
        handler.Requests.Count.ShouldBe(1);
        clock.TimersCreated.ShouldBe(0);
    }

    [Fact]
    public async Task WaitStopsWhenTheCallerCancels()
    {
        using var handler = new ScriptedProbeHandler().Then(HttpStatusCode.BadGateway);
        using var client = new HttpClient(handler, disposeHandler: false);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);
        var probe = CreateProbe(client, new AutoAdvanceTimeProvider(Start));

        var thrown = await Record.ExceptionAsync(
            async () => await probe.WaitUntilReadyAsync(Worker, cancelled.Token).ConfigureAwait(true)).ConfigureAwait(true);

        thrown.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public void InvalidSettingsAreRejected()
    {
        using var client = new HttpClient();
        var clock = new AutoAdvanceTimeProvider(Start);

        Should.Throw<ArgumentOutOfRangeException>(() => { _ = new WorkerReadinessProbe(client, clock, TimeSpan.Zero, TimeSpan.FromSeconds(2), "/healthz"); });
        Should.Throw<ArgumentOutOfRangeException>(() => { _ = new WorkerReadinessProbe(client, clock, TimeSpan.FromSeconds(60), TimeSpan.Zero, "/healthz"); });
        Should.Throw<ArgumentException>(() => { _ = new WorkerReadinessProbe(client, clock, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(2), " "); });
    }

    private static WorkerReadinessProbe CreateProbe(HttpClient client, TimeProvider clock) =>
        new(client, clock, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(2), "/healthz");
}
