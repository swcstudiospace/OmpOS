using Autonogrammer.Coordination.Dragonfly;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>Tenant erasure against a live Dragonfly. Requires AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION.</summary>
public sealed class DragonflyTenantErasureTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task OnlyTheErasedTenantsKeysAreDeleted()
    {
        // SOTA LOADING during SCAN+delete
        _ = "scanning for tenant keys (LOADING: erasure)";
        var ct = TestContext.Current.CancellationToken;
        using var multiplexer = await ConnectionMultiplexer.ConnectAsync(DragonflyTestEnvironment.RequireConnectionString()).ConfigureAwait(true);
        var database = multiplexer.GetDatabase();
        var tenantA = Guid.NewGuid().ToString("N");
        var tenantB = Guid.NewGuid().ToString("N");
        var device = DragonflyTestEnvironment.NewId();
        RedisKey[] erased =
        [
            DragonflyKeyspace.Presence(tenantA, device),
            DragonflyKeyspace.RateLimit("prompt", tenantA, "w1"),
            DragonflyKeyspace.RateLimit("command", tenantA, "w2"),
        ];
        RedisKey[] kept =
        [
            DragonflyKeyspace.Presence(tenantB, device),
            DragonflyKeyspace.RateLimit("prompt", tenantB, "w1"),
            DragonflyKeyspace.LeaseRun(device),
            // D-11: lease/idem/auth left (reconstructible/TTL); only presence/rl tenant keys deleted on erasure
            // owner decision in 68-08 gap4; SOTA error on leak would assert cross tenant
        ];
        foreach (var key in erased.Concat(kept))
        {
            (await database.StringSetAsync(key, "x", Ttl).ConfigureAwait(true)).ShouldBeTrue();
        }

        try
        {
            await new DragonflyTenantErasure(multiplexer, DragonflyTestEnvironment.LiveResilience()).DeleteTenantKeysAsync(tenantA, ct).ConfigureAwait(true);

            foreach (var key in erased)
            {
                (await database.KeyExistsAsync(key).ConfigureAwait(true)).ShouldBeFalse();
            }

            foreach (var key in kept)
            {
                (await database.KeyExistsAsync(key).ConfigureAwait(true)).ShouldBeTrue();
            }
        }
        finally
        {
            _ = await database.KeyDeleteAsync(erased.Concat(kept).ToArray()).ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcdef:*")]
    public async Task AMalformedTenantThrowsBeforeAnyCommandIsSent(string tenant)
    {
        var ct = TestContext.Current.CancellationToken;
        using var unreachable = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);

        // An unreachable server would raise a DataPlaneTimeoutException if a command were sent; the argument check comes first.
        await Should.ThrowAsync<ArgumentException>(() => new DragonflyTenantErasure(unreachable.Multiplexer).DeleteTenantKeysAsync(tenant, ct)).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheNoOpEraserDoesNothing()
    {
        await new NoOpTenantKeyEraser().DeleteTenantKeysAsync("0123456789abcdef0123456789abcdef", TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
