using Shouldly;
using Xunit;

namespace OmpOS.Coordination.Tests;

/// <summary>Lease semantics of the process-local store, driven by a clock that only moves when a test advances it.</summary>
public sealed class InMemorySessionRunLeasesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(10);

    /// <summary>Only one owner can hold a live lease.</summary>
    [Fact]
    public async Task AcquireIsExclusiveWhileTheLeaseIsLive()
    {
        var ct = TestContext.Current.CancellationToken;
        var leases = new InMemorySessionRunLeases(new ManualTimeProvider(Start));

        var first = await leases.TryAcquireAsync("s-1", "owner-a", Ttl, ct).ConfigureAwait(true);
        var second = await leases.TryAcquireAsync("s-1", "owner-b", Ttl, ct).ConfigureAwait(true);

        first.ShouldNotBeNull();
        second.ShouldBeNull();
        (await leases.IsHeldAsync("s-1", ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>Once a lease reaches its expiry, another owner can take it.</summary>
    [Fact]
    public async Task ExpiredLeaseCanBeTakenByAnotherOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var leases = new InMemorySessionRunLeases(clock);
        _ = await leases.TryAcquireAsync("s-1", "owner-a", Ttl, ct).ConfigureAwait(true);

        clock.Advance(Ttl);

        (await leases.IsHeldAsync("s-1", ct).ConfigureAwait(true)).ShouldBeFalse();
        var taken = await leases.TryAcquireAsync("s-1", "owner-b", Ttl, ct).ConfigureAwait(true);
        taken.ShouldNotBeNull();
    }

    /// <summary>Renewal restarts the lifetime from the renewal moment.</summary>
    [Fact]
    public async Task RenewExtendsTheLeaseFromTheRenewalMoment()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var leases = new InMemorySessionRunLeases(clock);
        var lease = (await leases.TryAcquireAsync("s-1", "owner-a", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        clock.Advance(TimeSpan.FromSeconds(6));
        (await lease.RenewAsync(Ttl, ct).ConfigureAwait(true)).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(6));

        (await leases.IsHeldAsync("s-1", ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>A lease that expired without renewal reports the loss.</summary>
    [Fact]
    public async Task RenewFailsOnceTheLeaseExpired()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var leases = new InMemorySessionRunLeases(clock);
        var lease = (await leases.TryAcquireAsync("s-1", "owner-a", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        clock.Advance(Ttl);

        (await lease.RenewAsync(Ttl, ct).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>After a takeover, the stale owner cannot renew, and the new owner still can.</summary>
    [Fact]
    public async Task RenewFailsAfterAnotherOwnerTookOver()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var leases = new InMemorySessionRunLeases(clock);
        var stale = (await leases.TryAcquireAsync("s-1", "owner-a", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        clock.Advance(Ttl);
        var current = (await leases.TryAcquireAsync("s-1", "owner-b", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        (await stale.RenewAsync(Ttl, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await current.RenewAsync(Ttl, ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>A stale owner's release must not remove the lease that another owner now holds.</summary>
    [Fact]
    public async Task ReleaseByNonOwnerIsNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(Start);
        var leases = new InMemorySessionRunLeases(clock);
        var stale = (await leases.TryAcquireAsync("s-1", "owner-a", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        clock.Advance(Ttl);
        var current = (await leases.TryAcquireAsync("s-1", "owner-b", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        await stale.DisposeAsync().ConfigureAwait(true);

        (await leases.IsHeldAsync("s-1", ct).ConfigureAwait(true)).ShouldBeTrue();
        (await current.RenewAsync(Ttl, ct).ConfigureAwait(true)).ShouldBeTrue();
    }

    /// <summary>The owner's release frees the session immediately.</summary>
    [Fact]
    public async Task ReleaseByOwnerFreesTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var leases = new InMemorySessionRunLeases(new ManualTimeProvider(Start));
        var lease = (await leases.TryAcquireAsync("s-1", "owner-a", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        await lease.DisposeAsync().ConfigureAwait(true);

        (await leases.IsHeldAsync("s-1", ct).ConfigureAwait(true)).ShouldBeFalse();
        var next = await leases.TryAcquireAsync("s-1", "owner-b", Ttl, ct).ConfigureAwait(true);
        next.ShouldNotBeNull();
    }

    /// <summary>A lifetime below one millisecond is rejected, matching the Dragonfly implementation.</summary>
    [Fact]
    public async Task TimeToLiveBelowOneMillisecondIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var leases = new InMemorySessionRunLeases(new ManualTimeProvider(Start));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => leases.TryAcquireAsync("s-1", "owner-a", TimeSpan.Zero, ct)).ConfigureAwait(true);
    }
}
