using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>
/// D-11 fail modes against a closed loopback port: leases, idempotency and nonces fail closed with a <see cref="DataPlaneTimeoutException"/>,
/// signals fail open. The shared test container is never stopped or restarted.
/// </summary>
public sealed class DragonflyFailModeTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task LeaseAcquireFailsClosedWithADragonflyTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var leases = new DragonflySessionRunLeases(server.Multiplexer);

        var failure = await Should.ThrowAsync<DataPlaneTimeoutException>(() => leases.TryAcquireAsync("cs-1", "owner", Ttl, ct)).ConfigureAwait(true);

        failure.Component.ShouldBe("dragonfly");
    }

    [Fact]
    public async Task LeaseIsHeldAndRenewFailClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var resilience = DragonflyResilience.Create();
        var leases = new DragonflySessionRunLeases(server.Multiplexer, resilience);
        var lease = new DragonflySessionRunLease(server.Multiplexer.GetDatabase(), resilience, "cs-1", "owner");

        try
        {
            await Should.ThrowAsync<DataPlaneTimeoutException>(() => leases.IsHeldAsync("cs-1", ct)).ConfigureAwait(true);
            await Should.ThrowAsync<DataPlaneTimeoutException>(() => lease.RenewAsync(Ttl, ct)).ConfigureAwait(true);
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ReleasingALeaseNeverThrowsBecauseTheLeaseExpiresByItself()
    {
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var lease = new DragonflySessionRunLease(server.Multiplexer.GetDatabase(), DragonflyResilience.Create(), "cs-1", "owner");

        await lease.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task IdempotencyClaimCompleteAndReleaseFailClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var store = new DragonflyIdempotencyStore(server.Multiplexer);

        await Should.ThrowAsync<DataPlaneTimeoutException>(() => store.ClaimAsync("scope", "key", "fp", ct)).ConfigureAwait(true);
        await Should.ThrowAsync<DataPlaneTimeoutException>(() => store.CompleteAsync("scope", "key", "fp", "{}", ct)).ConfigureAwait(true);
        await Should.ThrowAsync<DataPlaneTimeoutException>(() => store.ReleaseAsync("scope", "key", "fp", ct)).ConfigureAwait(true);
    }

    [Fact]
    public async Task SignalPublishAndSubscribeFailOpen()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var signals = new DragonflySessionSignals(server.Multiplexer);

        await signals.PublishAsync("cs-1", ct).ConfigureAwait(true);
        var subscription = await signals.SubscribeAsync((_, _) => ValueTask.CompletedTask, ct).ConfigureAwait(true);
        await subscription.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task TheHealthCheckReportsUnhealthyWithTheBreakerState()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var check = new DragonflyCoordinationHealthCheck(server.Multiplexer);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), ct).ConfigureAwait(true);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Data[DragonflyCoordinationHealthCheck.DependencyStateKey].ShouldBeOfType<string>().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RepeatedFailuresOpenTheBreakerAndLaterCallsAreRefusedWithoutReachingDragonfly()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var resilience = DragonflyResilience.Create(timeProvider: clock);
        var leases = new DragonflySessionRunLeases(server.Multiplexer, resilience);

        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<DataPlaneTimeoutException>(() => leases.TryAcquireAsync("cs-1", "owner", Ttl, ct)).ConfigureAwait(true);
        }

        resilience.State.ShouldBe(DependencyState.Open);
        var refused = await Should.ThrowAsync<DataPlaneTimeoutException>(() => leases.TryAcquireAsync("cs-1", "owner", Ttl, ct)).ConfigureAwait(true);
        refused.Reason.ShouldBe(DataPlaneTimeoutReason.CircuitOpen);

        clock.Advance(TimeSpan.FromSeconds(11));
        var probe = await Should.ThrowAsync<DataPlaneTimeoutException>(() => leases.TryAcquireAsync("cs-1", "owner", Ttl, ct)).ConfigureAwait(true);
        probe.Reason.ShouldBe(DataPlaneTimeoutReason.Timeout, "after the open window one probe reaches Dragonfly again");
    }

    [Fact]
    public async Task AClaimIsNeverRetriedButAnIdempotentReadIsRetriedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var resilience = DragonflyResilience.Create(timeProvider: new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        var claimCalls = 0;
        var readCalls = 0;

        await Should.ThrowAsync<DataPlaneTimeoutException>(
            () => resilience.ClosedAsync(
                false,
                () =>
                {
                    claimCalls++;
                    return Task.FromException<bool>(new IOException("refused"));
                },
                ct)).ConfigureAwait(true);
        await Should.ThrowAsync<DataPlaneTimeoutException>(
            () => resilience.ClosedAsync(
                true,
                () =>
                {
                    readCalls++;
                    return Task.FromException<bool>(new IOException("refused"));
                },
                ct)).ConfigureAwait(true);

        claimCalls.ShouldBe(1);
        readCalls.ShouldBe(2);
    }

    [Fact]
    public async Task AServerAnswerThatIsNotAnOutageReachesTheCallerUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var resilience = DragonflyResilience.Create();

        await Should.ThrowAsync<InvalidOperationException>(
            () => resilience.ClosedAsync(true, () => Task.FromException<bool>(new InvalidOperationException("script error")), ct)).ConfigureAwait(true);
    }
}
