using Shouldly;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>Wake-ups, single-use nonces and presence for the process-local dispatch seams.</summary>
public sealed class InMemoryDispatchCoordinationTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>A published device id reaches a subscriber and nothing else is delivered.</summary>
    [Fact]
    public async Task PublishWakesASubscriberWithTheDeviceIdOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var signals = new InMemoryDispatchSignals();
        var received = new List<string>();
        var subscription = await signals.SubscribeAsync((id, _) => { received.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);

        await signals.PublishAsync("device-1", ct).ConfigureAwait(true);

        received.ShouldBe(["device-1"]);
        await subscription.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>A publish with no subscriber succeeds and is not kept for a later subscriber.</summary>
    [Fact]
    public async Task PublishWithNoSubscriberIsANoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        var signals = new InMemoryDispatchSignals();
        var late = new List<string>();

        await Should.NotThrowAsync(() => signals.PublishAsync("device-1", ct)).ConfigureAwait(true);
        var subscription = await signals.SubscribeAsync((id, _) => { late.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);

        late.ShouldBeEmpty();
        await subscription.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>After disposal, later publishes skip the subscriber.</summary>
    [Fact]
    public async Task DisposeStopsDelivery()
    {
        var ct = TestContext.Current.CancellationToken;
        var signals = new InMemoryDispatchSignals();
        var received = new List<string>();
        var subscription = await signals.SubscribeAsync((id, _) => { received.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);

        await subscription.DisposeAsync().ConfigureAwait(true);
        await signals.PublishAsync("device-1", ct).ConfigureAwait(true);

        received.ShouldBeEmpty();
    }

    /// <summary>A nonce can be claimed once and consumed once. An expired nonce cannot be consumed.</summary>
    [Fact]
    public async Task NonceIsClaimedOnceAndConsumedOnceUntilItExpires()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero));
        var nonces = new InMemoryDispatchAuthNonces(clock);
        var hash = new string('a', 64);

        (await nonces.TryClaimAsync(hash, Ttl, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await nonces.TryClaimAsync(hash, Ttl, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await nonces.TryConsumeAsync(hash, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await nonces.TryConsumeAsync(hash, ct).ConfigureAwait(true)).ShouldBeFalse();

        (await nonces.TryClaimAsync(hash, Ttl, ct).ConfigureAwait(true)).ShouldBeTrue();
        clock.Advance(Ttl + TimeSpan.FromSeconds(1));
        (await nonces.TryConsumeAsync(hash, ct).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Set, refresh, remove and expiry follow the injected clock. Another holder cannot refresh.</summary>
    [Fact]
    public async Task PresenceFollowsTheHolderAndTheClock()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero));
        var presence = new InMemoryDispatchPresence(clock);
        var ttl = TimeSpan.FromSeconds(45);

        (await presence.GetAsync("acct-1", "device-1", ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Offline);

        await presence.SetAsync("acct-1", "device-1", "inst:conn", ttl, ct).ConfigureAwait(true);
        (await presence.GetAsync("acct-1", "device-1", ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Online("inst:conn"));
        (await presence.RefreshAsync("acct-1", "device-1", "other:conn", ttl, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await presence.GetAsync("acct-1", "device-1", ct).ConfigureAwait(true)).Holder.ShouldBe("inst:conn");
        (await presence.RefreshAsync("acct-1", "device-1", "inst:conn", ttl, ct).ConfigureAwait(true)).ShouldBeTrue();

        clock.Advance(ttl + TimeSpan.FromSeconds(1));
        (await presence.GetAsync("acct-1", "device-1", ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Offline);
        (await presence.RefreshAsync("acct-1", "device-1", "inst:conn", ttl, ct).ConfigureAwait(true)).ShouldBeFalse();

        await presence.SetAsync("acct-1", "device-1", "inst:conn", ttl, ct).ConfigureAwait(true);
        await presence.RemoveAsync("acct-1", "device-1", ct).ConfigureAwait(true);
        (await presence.GetAsync("acct-1", "device-1", ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Offline);
    }
}
