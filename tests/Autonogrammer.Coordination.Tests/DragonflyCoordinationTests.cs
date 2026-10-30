using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Coordination.Dragonfly;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>
/// Cross-instance behaviour against a live Dragonfly. Each test opens its own multiplexers, so the tests prove that the
/// coordination state lives in the server and not in the process. Every test requires AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION.
/// </summary>
public sealed class DragonflyCoordinationTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    /// <summary>Two connections race for many sessions at once, and each session has exactly one winner.</summary>
    [Fact]
    public async Task TwoMultiplexersContendForOneLeaseAndExactlyOneWins()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await OpenAsync().ConfigureAwait(true);
        using var second = await OpenAsync().ConfigureAwait(true);
        var leasesA = new DragonflySessionRunLeases(first);
        var leasesB = new DragonflySessionRunLeases(second);

        var sessionIds = Enumerable.Range(0, 20).Select(_ => DragonflyTestEnvironment.NewId()).ToArray();
        var attempts = sessionIds
            .SelectMany(id => new[]
            {
                leasesA.TryAcquireAsync(id, "owner-a", Ttl, ct),
                leasesB.TryAcquireAsync(id, "owner-b", Ttl, ct),
            })
            .ToArray();
        var results = await Task.WhenAll(attempts).ConfigureAwait(true);

        // results[2i] is owner A's attempt for session i, and results[2i + 1] is owner B's attempt for the same session.
        var winners = new List<ISessionRunLease>();
        for (var session = 0; session < sessionIds.Length; session++)
        {
            var a = results[2 * session];
            var b = results[(2 * session) + 1];
            ((a is null) != (b is null)).ShouldBeTrue("exactly one owner must win each session");
            winners.Add((a ?? b).ShouldNotBeNull());
        }

        foreach (var winner in winners)
        {
            await winner.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Renewal keeps a lease alive past its original expiry, and the loss is reported once it lapses.</summary>
    [Fact]
    public async Task RenewExtendsTheLeaseAndLossIsDetectedAfterExpiry()
    {
        var ct = TestContext.Current.CancellationToken;
        using var multiplexer = await OpenAsync().ConfigureAwait(true);
        var leases = new DragonflySessionRunLeases(multiplexer);
        var sessionId = DragonflyTestEnvironment.NewId();
        var ttl = TimeSpan.FromSeconds(2);
        var lease = (await leases.TryAcquireAsync(sessionId, "owner-a", ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        // Renewing every 600 ms keeps a 2 s lease alive for 3 s, well past its original expiry.
        for (var renewal = 0; renewal < 5; renewal++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(600), ct).ConfigureAwait(true);
            (await lease.RenewAsync(ttl, ct).ConfigureAwait(true)).ShouldBeTrue("a live lease must renew");
        }

        // Without renewal the lease expires, and the next renewal reports the loss.
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(true);
        (await lease.RenewAsync(ttl, ct).ConfigureAwait(true)).ShouldBeFalse("an expired lease must not renew");
        (await leases.IsHeldAsync(sessionId, ct).ConfigureAwait(true)).ShouldBeFalse();
        await lease.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>A stale owner's release must not remove the lease that a newer owner holds on another connection.</summary>
    [Fact]
    public async Task ReleaseByNonOwnerDoesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var multiplexer = await OpenAsync().ConfigureAwait(true);
        var leases = new DragonflySessionRunLeases(multiplexer);
        var sessionId = DragonflyTestEnvironment.NewId();
        var stale = (await leases.TryAcquireAsync(sessionId, "owner-a", TimeSpan.FromSeconds(1), ct).ConfigureAwait(true)).ShouldNotBeNull();

        await Task.Delay(TimeSpan.FromMilliseconds(1500), ct).ConfigureAwait(true);
        var current = (await leases.TryAcquireAsync(sessionId, "owner-b", Ttl, ct).ConfigureAwait(true)).ShouldNotBeNull();

        await stale.DisposeAsync().ConfigureAwait(true);

        (await leases.IsHeldAsync(sessionId, ct).ConfigureAwait(true)).ShouldBeTrue();
        (await current.RenewAsync(Ttl, ct).ConfigureAwait(true)).ShouldBeTrue();
        await current.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>The first claim wins across two instances, and the completed response replays on the other.</summary>
    [Fact]
    public async Task FirstIdempotencyClaimWinsAcrossTwoInstances()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await OpenAsync().ConfigureAwait(true);
        using var second = await OpenAsync().ConfigureAwait(true);
        var storeA = new DragonflyIdempotencyStore(first);
        var storeB = new DragonflyIdempotencyStore(second);
        var scope = "session-start";
        var key = DragonflyTestEnvironment.NewId();
        const string fingerprint = "fp-1";

        var claims = await Task.WhenAll(
            storeA.ClaimAsync(scope, key, fingerprint, ct),
            storeB.ClaimAsync(scope, key, fingerprint, ct)).ConfigureAwait(true);
        claims.Count(claim => claim.Outcome == IdempotencyOutcome.Claimed).ShouldBe(1);
        claims.Count(claim => claim.Outcome == IdempotencyOutcome.InProgress).ShouldBe(1);

        const string responseJson = """{"sessionId":"ok"}""";
        await storeA.CompleteAsync(scope, key, fingerprint, responseJson, ct).ConfigureAwait(true);

        var replay = await storeB.ClaimAsync(scope, key, fingerprint, ct).ConfigureAwait(true);
        replay.Outcome.ShouldBe(IdempotencyOutcome.Replay);
        replay.ResponseJson.ShouldBe(responseJson);

        var conflict = await storeA.ClaimAsync(scope, key, "fp-2", ct).ConfigureAwait(true);
        conflict.Outcome.ShouldBe(IdempotencyOutcome.Conflict);
    }

    /// <summary>A pending claim released on one instance can be claimed by the other.</summary>
    [Fact]
    public async Task ReleaseReopensAPendingClaimForAnotherInstance()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await OpenAsync().ConfigureAwait(true);
        using var second = await OpenAsync().ConfigureAwait(true);
        var storeA = new DragonflyIdempotencyStore(first);
        var storeB = new DragonflyIdempotencyStore(second);
        var scope = "session-start";
        var key = DragonflyTestEnvironment.NewId();

        (await storeA.ClaimAsync(scope, key, "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);
        await storeA.ReleaseAsync(scope, key, "fp-1", ct).ConfigureAwait(true);

        (await storeB.ClaimAsync(scope, key, "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);
    }

    /// <summary>A signal published on one connection reaches a subscriber on another.</summary>
    [Fact]
    public async Task SignalPublishedByOneInstanceReachesAnother()
    {
        var ct = TestContext.Current.CancellationToken;
        using var subscriberConnection = await OpenAsync().ConfigureAwait(true);
        using var publisherConnection = await OpenAsync().ConfigureAwait(true);
        var subscriber = new DragonflySessionSignals(subscriberConnection);
        var publisher = new DragonflySessionSignals(publisherConnection);
        var sessionId = DragonflyTestEnvironment.NewId();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await subscriber.SubscribeAsync(
            (id, ignoredToken) =>
            {
                if (string.Equals(id, sessionId, StringComparison.Ordinal))
                {
                    received.TrySetResult(id);
                }

                return ValueTask.CompletedTask;
            },
            ct).ConfigureAwait(true);

        await publisher.PublishAsync(sessionId, ct).ConfigureAwait(true);
        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        delivered.ShouldBe(sessionId);
        await subscription.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// A completion that arrives after its pending claim expired must not overwrite a newer claim. The newer claim uses a
    /// different fingerprint, so the compare-and-set finds P|fp-2 and does nothing for fp-1.
    /// </summary>
    [Fact]
    public async Task CompleteAfterPendingClaimExpiredDoesNotOverwriteNewerClaim()
    {
        var ct = TestContext.Current.CancellationToken;
        using var first = await OpenAsync().ConfigureAwait(true);
        using var second = await OpenAsync().ConfigureAwait(true);
        var storeA = new DragonflyIdempotencyStore(first);
        var storeB = new DragonflyIdempotencyStore(second);
        var scope = "session-start";
        var key = DragonflyTestEnvironment.NewId();

        (await storeA.ClaimAsync(scope, key, "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);

        // Expire the pending record on the server, as its 60 s lifetime would after the claim goes stale.
        await first.GetDatabase().KeyExpireAsync(IdempotencyKey(scope, key), TimeSpan.FromMilliseconds(200)).ConfigureAwait(true);
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(true);

        (await storeB.ClaimAsync(scope, key, "fp-2", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Claimed);

        // The stale completion for fp-1 must not replace the newer pending claim for fp-2.
        await storeA.CompleteAsync(scope, key, "fp-1", """{"stale":true}""", ct).ConfigureAwait(true);

        (await storeB.ClaimAsync(scope, key, "fp-2", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.InProgress);
        (await storeA.ClaimAsync(scope, key, "fp-1", ct).ConfigureAwait(true)).Outcome.ShouldBe(IdempotencyOutcome.Conflict);
    }

    /// <summary>The registration resolves all three services over one shared multiplexer, and it connects to the server.</summary>
    [Fact]
    public void AddDragonflyCoordinationRegistersThreeServicesOverOneMultiplexer()
    {
        var services = new ServiceCollection();
        services.AddDragonflyCoordination(DragonflyTestEnvironment.RequireConnectionString());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISessionRunLeases>().ShouldBeOfType<DragonflySessionRunLeases>();
        provider.GetRequiredService<ISessionSignals>().ShouldBeOfType<DragonflySessionSignals>();
        provider.GetRequiredService<IIdempotencyStore>().ShouldBeOfType<DragonflyIdempotencyStore>();
        provider.GetRequiredService<IConnectionMultiplexer>().ShouldBeSameAs(provider.GetRequiredService<IConnectionMultiplexer>());
    }

    /// <summary>Opens a multiplexer to the Dragonfly test server. Each test owns the connections it opens.</summary>
    /// <returns>A connected multiplexer.</returns>
    private static async Task<ConnectionMultiplexer> OpenAsync()
    {
        return await ConnectionMultiplexer.ConnectAsync(DragonflyTestEnvironment.RequireConnectionString()).ConfigureAwait(true);
    }

    /// <summary>Returns the Dragonfly key the store uses for a scope and key, following the documented layout.</summary>
    private static RedisKey IdempotencyKey(string scope, string key)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(scope + "\n" + key));
        return "autonogrammer:idem:" + Convert.ToHexStringLower(digest);
    }
}
