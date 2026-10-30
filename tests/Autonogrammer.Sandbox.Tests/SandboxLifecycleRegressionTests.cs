using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Regression tests for lifecycle TTL boundaries, typed store failures, and jitter bounds (WR-06, WR-07, WR-12).
/// </summary>
public sealed class SandboxLifecycleRegressionTests
{
    private static ProvisionRequest CreateProvisionRequest() => new()
    {
        IdempotencyKey = $"test-key-{Guid.NewGuid():N}",
        SessionId = $"session-{Guid.NewGuid():N}",
        Timeout = TimeSpan.FromMinutes(5),
        Isolation = Isolation.Container,
        NetworkPolicy = NetworkPolicy.DefaultDeny,
        Mounts = Array.Empty<MountSpec>(),
        SecretRefs = Array.Empty<SecretRef>(),
        Spec = new SandboxSpec
        {
            Image = "alpine:latest",
            Command = ["sleep", "3600"],
            WorkingDirectory = "/workspace",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Limits = null,
        },
    };

    /// <summary>
    /// A sandbox exactly at its TTL is reaped on the next sweep (WR-06).
    /// </summary>
    [Fact]
    public async Task ReapOnce_ReapsSandboxExactlyAtTtl()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();
        var options = Options.Create(new SandboxLifecycleOptions
        {
            Ttl = TimeSpan.FromMinutes(30),
            TeardownRetryCount = 3,
            BaseBackoff = TimeSpan.FromMilliseconds(10),
        });
        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, NullLogger<SandboxLifecycle>.Instance, new Random(42));

        var request = CreateProvisionRequest();
        var ct = TestContext.Current.CancellationToken;
        await lifecycle.ProvisionAsync(request, static _ => Task.FromResult<string?>("Running"), ct);

        fakeTime.Advance(TimeSpan.FromMinutes(30));

        var reaped = await lifecycle.ReapOnceAsync(fakeTime.GetUtcNow(), ct);

        reaped.ShouldBe(1);
    }

    /// <summary>
    /// A session-store outage surfaces as a typed provisioning failure, not a raw store error (WR-12).
    /// </summary>
    [Fact]
    public async Task Provision_SessionStoreFailure_WrappedAsTyped()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();
        var options = Options.Create(new SandboxLifecycleOptions
        {
            Ttl = TimeSpan.FromMinutes(30),
            TeardownRetryCount = 1,
            BaseBackoff = TimeSpan.FromMilliseconds(1),
        });
        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, NullLogger<SandboxLifecycle>.Instance, new Random(42));

        var ct = TestContext.Current.CancellationToken;
        var exception = await Should.ThrowAsync<ProvisionTimeoutException>(() =>
            lifecycle.ProvisionAsync(CreateProvisionRequest(), static _ => Task.FromException<string?>(new InvalidOperationException("store down")), ct));

        exception.InnerException.ShouldNotBeNull();
    }

    /// <summary>
    /// Equal jitter never collapses toward zero: every draw stays within [base/2, base] (WR-07).
    /// </summary>
    [Fact]
    public void EqualJitterDelay_StaysWithinHalfToFullBase()
    {
        var random = new Random(42);

        for (var i = 0; i < 1000; i++)
        {
            var delay = SandboxLifecycle.ComputeEqualJitterDelay(1000, random);
            delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(500));
            delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(1000));
        }
    }

    /// <summary>
    /// The jitter total is capped at 30 seconds (WR-07).
    /// </summary>
    [Fact]
    public void EqualJitterDelay_CappedAtThirtySeconds()
    {
        var random = new Random(7);

        for (var i = 0; i < 100; i++)
        {
            var delay = SandboxLifecycle.ComputeEqualJitterDelay(1_000_000, random);
            delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
            delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(30));
        }
    }
}
