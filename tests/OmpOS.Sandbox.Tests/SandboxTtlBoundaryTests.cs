using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Pins the TTL expiry boundary: a sandbox is expired exactly when now &gt;= provisionedAt + ttl (WR-06).
/// </summary>
public sealed class SandboxTtlBoundaryTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// One tick before provisionedAt + ttl the sandbox is not yet expired and survives the sweep (WR-06).
    /// </summary>
    [Fact]
    public async Task ReapOnce_OneTickBeforeExpiry_KeepsSandbox()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var lifecycle = LifecycleTestRequests.CreateLifecycle(new FakeSandboxProvider(), clock, Ttl);
        var ct = TestContext.Current.CancellationToken;
        await lifecycle.ProvisionAsync(LifecycleTestRequests.Create(), static _ => Task.FromResult<string?>("Running"), ct);

        clock.Advance(Ttl - TimeSpan.FromTicks(1));
        var reaped = await lifecycle.ReapOnceAsync(clock.GetUtcNow(), ct);

        reaped.ShouldBe(0);
        lifecycle.GetAllEntries().Count.ShouldBe(1);
    }

    /// <summary>
    /// Exactly at provisionedAt + ttl the sandbox is expired and reaped; the sweep at the instant after is a no-op (WR-06).
    /// </summary>
    [Fact]
    public async Task ReapOnce_ExactlyAtExpiry_ReapsSandbox()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var lifecycle = LifecycleTestRequests.CreateLifecycle(new FakeSandboxProvider(), clock, Ttl);
        var ct = TestContext.Current.CancellationToken;
        await lifecycle.ProvisionAsync(LifecycleTestRequests.Create(), static _ => Task.FromResult<string?>("Running"), ct);

        clock.Advance(Ttl - TimeSpan.FromTicks(1));
        (await lifecycle.ReapOnceAsync(clock.GetUtcNow(), ct)).ShouldBe(0);

        clock.Advance(TimeSpan.FromTicks(1));
        var reaped = await lifecycle.ReapOnceAsync(clock.GetUtcNow(), ct);

        reaped.ShouldBe(1);
        lifecycle.GetAllEntries().ShouldBeEmpty();
    }
}
