using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Session-store failures surface as the lifecycle's typed exception with the store error preserved (WR-12).
/// </summary>
public sealed class SandboxStoreFailureTypedTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    /// <summary>
    /// A throwing session store during provisioning is wrapped as ProvisionTimeoutException with the original store exception as inner (WR-12).
    /// </summary>
    [Fact]
    public async Task Provision_StoreThrows_WrappedWithOriginalInner()
    {
        var storeFailure = new IOException("session store unreachable");
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var lifecycle = LifecycleTestRequests.CreateLifecycle(new FakeSandboxProvider(), clock, Ttl);
        var ct = TestContext.Current.CancellationToken;

        var exception = await Should.ThrowAsync<ProvisionTimeoutException>(() =>
            lifecycle.ProvisionAsync(LifecycleTestRequests.Create(), _ => Task.FromException<string?>(storeFailure), ct));

        exception.InnerException.ShouldBeSameAs(storeFailure);
        lifecycle.GetAllEntries().ShouldBeEmpty();
    }

    /// <summary>
    /// A session-store outage during the reap sweep is contained: the sweep completes, reaps nothing, and keeps the sandbox registered (WR-12).
    /// </summary>
    [Fact]
    public async Task ReapOnce_StoreFailure_IsContainedAndKeepsSandbox()
    {
        var storeFailure = new IOException("session store unreachable");
        var storeHealthy = true;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var lifecycle = LifecycleTestRequests.CreateLifecycle(new FakeSandboxProvider(), clock, Ttl);
        var ct = TestContext.Current.CancellationToken;
        await lifecycle.ProvisionAsync(
            LifecycleTestRequests.Create(),
            _ => storeHealthy ? Task.FromResult<string?>("Running") : Task.FromException<string?>(storeFailure),
            ct);

        storeHealthy = false;
        var reaped = await lifecycle.ReapOnceAsync(clock.GetUtcNow(), ct);

        reaped.ShouldBe(0);
        lifecycle.GetAllEntries().Count.ShouldBe(1);
    }
}
