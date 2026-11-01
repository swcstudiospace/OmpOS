using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>Dispatch nonces, presence and signals against the throwaway Dragonfly, plus the closed and open fail modes.</summary>
public sealed class DragonflyDispatchCoordinationTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    /// <summary>A nonce is claimed once and consumed once across two connections. The key is the keyspace nonce key and carries a TTL.</summary>
    [Fact]
    public async Task NonceIsSingleUseAcrossReplicasAndExpiresWithItsClaim()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await OpenAsync().ConfigureAwait(true);
        using var second = await OpenAsync().ConfigureAwait(true);
        var resilience = DragonflyTestEnvironment.LiveResilience();
        var storeA = new DragonflyDispatchAuthNonces(first, resilience);
        var storeB = new DragonflyDispatchAuthNonces(second, resilience);
        var hash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var key = DragonflyKeyspace.AuthNonce(hash);

        (await storeA.TryClaimAsync(hash, Ttl, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await storeB.TryClaimAsync(hash, Ttl, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await first.GetDatabase().KeyTimeToLiveAsync(key).ConfigureAwait(true)).ShouldNotBeNull().ShouldBeLessThanOrEqualTo(Ttl);
        (await storeB.TryConsumeAsync(hash, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await storeA.TryConsumeAsync(hash, ct).ConfigureAwait(true)).ShouldBeFalse();

        (await storeA.TryClaimAsync(hash, TimeSpan.FromMilliseconds(200), ct).ConfigureAwait(true)).ShouldBeTrue();
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(true);
        (await storeB.TryConsumeAsync(hash, ct).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Presence is one key built by the keyspace, refreshed only by its holder, and removed on revoke.</summary>
    [Fact]
    public async Task PresenceKeyCarriesTheHolderAndTtlInOneWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        using var multiplexer = await OpenAsync().ConfigureAwait(true);
        var hasher = new TenantHasher(new TenantHasherOptions { Pepper = "dispatch-presence-test" });
        var presence = new DragonflyDispatchPresence(multiplexer, hasher, DragonflyTestEnvironment.LiveResilience());
        var accountId = DragonflyTestEnvironment.NewId();
        var deviceId = DragonflyTestEnvironment.NewId();
        var key = DragonflyKeyspace.Presence(hasher.HashTenant(accountId), deviceId);
        var ttl = TimeSpan.FromSeconds(45);

        (await presence.GetAsync(accountId, deviceId, ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Offline);
        await presence.SetAsync(accountId, deviceId, "inst:conn", ttl, ct).ConfigureAwait(true);

        var database = multiplexer.GetDatabase();
        (await database.StringGetAsync(key).ConfigureAwait(true)).ToString().ShouldBe("inst:conn");
        (await database.KeyTimeToLiveAsync(key).ConfigureAwait(true)).ShouldNotBeNull().ShouldBeLessThanOrEqualTo(ttl);
        (await presence.GetAsync(accountId, deviceId, ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Online("inst:conn"));
        (await presence.RefreshAsync(accountId, deviceId, "other:conn", ttl, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await database.StringGetAsync(key).ConfigureAwait(true)).ToString().ShouldBe("inst:conn");
        (await presence.RefreshAsync(accountId, deviceId, "inst:conn", ttl, ct).ConfigureAwait(true)).ShouldBeTrue();

        await presence.RemoveAsync(accountId, deviceId, ct).ConfigureAwait(true);
        (await presence.GetAsync(accountId, deviceId, ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Offline);
        (await database.KeyExistsAsync(key).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>A device id published on the dispatch channel wakes a subscriber on another connection.</summary>
    [Fact]
    public async Task SignalWakesASubscriberWithTheDeviceIdOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        using var subscriberConnection = await OpenAsync().ConfigureAwait(true);
        using var publisherConnection = await OpenAsync().ConfigureAwait(true);
        var subscriber = new DragonflyDispatchSignals(subscriberConnection, resilience: DragonflyTestEnvironment.LiveResilience());
        var publisher = new DragonflyDispatchSignals(publisherConnection, resilience: DragonflyTestEnvironment.LiveResilience());
        var deviceId = DragonflyTestEnvironment.NewId();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = await subscriber.SubscribeAsync(
            (id, ignoredToken) =>
            {
                if (string.Equals(id, deviceId, StringComparison.Ordinal))
                {
                    received.TrySetResult(id);
                }

                return ValueTask.CompletedTask;
            },
            ct).ConfigureAwait(true);

        await publisher.PublishAsync(deviceId, ct).ConfigureAwait(true);
        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        delivered.ShouldBe(deviceId);
        await subscription.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>An unreachable Dragonfly is unknown for presence and closed for nonces.</summary>
    [Fact]
    public async Task UnreachableDragonflyIsUnknownForPresenceAndClosedForNonces()
    {
        var ct = TestContext.Current.CancellationToken;
        using var server = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var presence = new DragonflyDispatchPresence(server.Multiplexer, new TenantHasher(new TenantHasherOptions { Pepper = "dispatch-presence-test" }));
        var nonces = new DragonflyDispatchAuthNonces(server.Multiplexer);
        var hash = new string('b', 64);

        (await presence.GetAsync("acct-1", "device-1", ct).ConfigureAwait(true)).ShouldBe(DispatchPresence.Unknown);
        await Should.NotThrowAsync(() => presence.SetAsync("acct-1", "device-1", "inst:conn", TimeSpan.FromSeconds(45), ct)).ConfigureAwait(true);
        (await presence.RefreshAsync("acct-1", "device-1", "inst:conn", TimeSpan.FromSeconds(45), ct).ConfigureAwait(true)).ShouldBeFalse();
        await Should.NotThrowAsync(() => presence.RemoveAsync("acct-1", "device-1", ct)).ConfigureAwait(true);

        var claim = await Should.ThrowAsync<DataPlaneTimeoutException>(() => nonces.TryClaimAsync(hash, Ttl, ct)).ConfigureAwait(true);
        var consume = await Should.ThrowAsync<DataPlaneTimeoutException>(() => nonces.TryConsumeAsync(hash, ct)).ConfigureAwait(true);
        claim.Component.ShouldBe("dragonfly");
        consume.Component.ShouldBe("dragonfly");
    }

    private static async Task<ConnectionMultiplexer> OpenAsync()
    {
        return await ConnectionMultiplexer.ConnectAsync(DragonflyTestEnvironment.RequireConnectionString()).ConfigureAwait(true);
    }
}
