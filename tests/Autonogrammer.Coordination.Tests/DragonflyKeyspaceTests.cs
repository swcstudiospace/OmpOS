using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Coordination.Dragonfly;
using Shouldly;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>The keyspace helper builds every key under <c>autonogrammer:v1:</c> and refuses a write without a bounded lifetime. No server needed.</summary>
public sealed class DragonflyKeyspaceTests
{
    private const string Tenant = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void ThePrefixIsTheV1Namespace()
    {
        DragonflyKeyspace.Prefix.ShouldBe("autonogrammer:v1:");
    }

    [Fact]
    public void EveryBuilderStartsWithThePrefixAndUsesItsDomain()
    {
        var nonce = new string('a', 64);

        ((string?)DragonflyKeyspace.LeaseRun("cs-1")).ShouldBe("autonogrammer:v1:lease:run:cs-1");
        ((string?)DragonflyKeyspace.Presence(Tenant, "dev_1")).ShouldBe("autonogrammer:v1:presence:" + Tenant + ":dev_1");
        ((string?)DragonflyKeyspace.RateLimit("prompt", Tenant, "w1")).ShouldBe("autonogrammer:v1:rl:prompt:" + Tenant + ":w1");
        ((string?)DragonflyKeyspace.AuthNonce(nonce)).ShouldBe("autonogrammer:v1:auth:nonce:" + nonce);
        ((string?)DragonflyKeyspace.SignalSession).ShouldBe("autonogrammer:v1:sig:session");
        ((string?)DragonflyKeyspace.SignalDispatch).ShouldBe("autonogrammer:v1:sig:dispatch");
    }

    [Fact]
    public void TheIdempotencyKeyIsTheSha256OfScopeNewlineKey()
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("scope\nkey")));

        ((string?)DragonflyKeyspace.Idempotency("scope", "key")).ShouldBe("autonogrammer:v1:idem:" + digest);
        ((string?)DragonflyKeyspace.Idempotency("scope", "a:b/ c\u00e9")).ShouldNotBeNull().Contains("a:b", StringComparison.Ordinal).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("cs:1")]
    [InlineData("cs.1")]
    [InlineData("cs 1")]
    [InlineData("*")]
    public void AnIdWithKeySyntaxIsRefused(string sessionId)
    {
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.LeaseRun(sessionId));
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.Presence(Tenant, sessionId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcde*")]
    [InlineData("user@example.com")]
    public void ATenantThatIsNot32LowercaseHexIsRefusedBeforeItJoinsAKeyOrAPattern(string tenant)
    {
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.RequireTenant(tenant));
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.Presence(tenant, "dev"));
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.RateLimit("p", tenant, "w"));
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.TenantScanPattern(tenant));
    }

    [Fact]
    public void ANonceThatIsNotA64CharacterLowercaseHashIsRefused()
    {
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.AuthNonce("abc"));
        Should.Throw<ArgumentException>(() => DragonflyKeyspace.AuthNonce(new string('A', 64)));
    }

    [Fact]
    public void TheScanPatternIsTheOnlyTenantWideMatch()
    {
        DragonflyKeyspace.TenantScanPattern(Tenant).ShouldBe("autonogrammer:v1:*:" + Tenant + ":*");
    }

    [Fact]
    public void ALifetimeOfAtMost24HoursIsAccepted()
    {
        DragonflyKeyspace.RequireTimeToLive(TimeSpan.FromMilliseconds(1)).ShouldBe(TimeSpan.FromMilliseconds(1));
        DragonflyKeyspace.RequireTimeToLive(TimeSpan.FromHours(24)).ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public void AMissingZeroNegativeOrOver24HourLifetimeIsRefused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DragonflyKeyspace.RequireTimeToLive(null));
        Should.Throw<ArgumentOutOfRangeException>(() => DragonflyKeyspace.RequireTimeToLive(TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => DragonflyKeyspace.RequireTimeToLive(TimeSpan.FromSeconds(-1)));
        Should.Throw<ArgumentOutOfRangeException>(() => DragonflyKeyspace.RequireTimeToLive(TimeSpan.FromHours(24) + TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public async Task ALeaseOrRenewalAbove24HoursIsRefusedBeforeAnyCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        using var unreachable = await UnreachableDragonfly.OpenAsync().ConfigureAwait(true);
        var leases = new DragonflySessionRunLeases(unreachable.Multiplexer);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => leases.TryAcquireAsync("cs-1", "owner", TimeSpan.FromHours(25), ct)).ConfigureAwait(true);
        var lease = new DragonflySessionRunLease(unreachable.Multiplexer.GetDatabase(), DragonflyResilience.Create(), "cs-1", "owner");
        try
        {
            await Should.ThrowAsync<ArgumentOutOfRangeException>(() => lease.RenewAsync(TimeSpan.FromHours(25), ct)).ConfigureAwait(true);
        }
        finally
        {
            await lease.DisposeAsync().ConfigureAwait(true);
        }
    }
}
