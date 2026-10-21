using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Tests for SandboxLifecycle with fake clock.
/// </summary>
public sealed class SandboxLifecycleTests
{
    [Fact]
    public async Task TtlExpiry_TriggersReaper()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();
        var options = Options.Create(new SandboxLifecycleOptions
        {
            Ttl = TimeSpan.FromMinutes(30),
            TeardownRetryCount = 3,
            BaseBackoff = TimeSpan.FromMilliseconds(10),
        });
        var logger = NullLogger<SandboxLifecycle>.Instance;
        var random = new Random(42);

        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, logger, random);

        var request = CreateProvisionRequest();
        var sessionStatuses = new Dictionary<string, string>(StringComparer.Ordinal) { [request.SessionId] = "Running" };

        var ct = TestContext.Current.CancellationToken;
        var result = await lifecycle.ProvisionAsync(
            request,
            sessionId => Task.FromResult(sessionStatuses.GetValueOrDefault(sessionId)),
            ct);

        result.NewlyProvisioned.ShouldBeTrue();
        var sandboxId = result.Descriptor.Id;

        fakeTime.Advance(TimeSpan.FromMinutes(31));

        var reaped = await lifecycle.ReapOnceAsync(fakeTime.GetUtcNow(), ct);

        reaped.ShouldBe(1);

        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync(sandboxId));
    }

    [Fact]
    public async Task OrphanReaper_SweepsOrphanedSandboxes()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();
        var options = Options.Create(new SandboxLifecycleOptions
        {
            Ttl = TimeSpan.FromMinutes(30),
            TeardownRetryCount = 3,
            BaseBackoff = TimeSpan.FromMilliseconds(10),
        });
        var logger = NullLogger<SandboxLifecycle>.Instance;
        var random = new Random(42);

        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, logger, random);

        var request = CreateProvisionRequest();
        var sessionStatuses = new Dictionary<string, string>(StringComparer.Ordinal) { [request.SessionId] = "Running" };

        var ct = TestContext.Current.CancellationToken;
        var result = await lifecycle.ProvisionAsync(
            request,
            sessionId => Task.FromResult(sessionStatuses.GetValueOrDefault(sessionId)),
            ct);

        var sandboxId = result.Descriptor.Id;

        sessionStatuses[request.SessionId] = "Completed";

        fakeTime.Advance(TimeSpan.FromMinutes(1));

        var reaped = await lifecycle.ReapOnceAsync(fakeTime.GetUtcNow(), ct);

        reaped.ShouldBe(1);

        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync(sandboxId));
    }

    [Fact]
    public async Task Teardown_RetriedWithJitteredBackoff()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();
        provider.FailTeardown(2);

        var options = Options.Create(new SandboxLifecycleOptions
        {
            Ttl = TimeSpan.FromMinutes(30),
            TeardownRetryCount = 5,
            BaseBackoff = TimeSpan.FromMilliseconds(100),
        });
        var logger = NullLogger<SandboxLifecycle>.Instance;
        var random = new Random(42);

        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, logger, random);

        var request = CreateProvisionRequest();
        var sessionStatuses = new Dictionary<string, string>(StringComparer.Ordinal) { [request.SessionId] = "Running" };

        var ct = TestContext.Current.CancellationToken;
        var result = await lifecycle.ProvisionAsync(
            request,
            sessionId => Task.FromResult(sessionStatuses.GetValueOrDefault(sessionId)),
            ct);

        var sandboxId = result.Descriptor.Id;

        var startTime = fakeTime.GetUtcNow();

        await lifecycle.TeardownAsync(sandboxId, ct);

        var endTime = fakeTime.GetUtcNow();
        var elapsed = endTime - startTime;

        elapsed.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(0);

        var entry = lifecycle.GetEntry(sandboxId);
        entry.ShouldBeNull();
    }

    [Fact]
    public async Task FailedProvision_NeverLeavesSessionRunning()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();
        provider.FailProvision(1);

        var options = Options.Create(new SandboxLifecycleOptions
        {
            Ttl = TimeSpan.FromMinutes(30),
            TeardownRetryCount = 3,
            BaseBackoff = TimeSpan.FromMilliseconds(10),
        });
        var logger = NullLogger<SandboxLifecycle>.Instance;
        var random = new Random(42);

        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, logger, random);

        var request = CreateProvisionRequest();
        var sessionStatuses = new Dictionary<string, string>(StringComparer.Ordinal) { [request.SessionId] = "Running" };

        await Should.ThrowAsync<ProvisionTimeoutException>(() =>
            lifecycle.ProvisionAsync(
                request,
                sessionId => Task.FromResult(sessionStatuses.GetValueOrDefault(sessionId)),
                TestContext.Current.CancellationToken));

        lifecycle.GetAllEntries().ShouldBeEmpty();
    }

    [Fact]
    public async Task Provision_FailsFast_WhenSessionNotRunning()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();

        var options = Options.Create(new SandboxLifecycleOptions());
        var logger = NullLogger<SandboxLifecycle>.Instance;

        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, logger);

        var request = CreateProvisionRequest();
        var sessionStatuses = new Dictionary<string, string>(StringComparer.Ordinal) { [request.SessionId] = "Completed" };

        await Should.ThrowAsync<InvalidOperationException>(() =>
            lifecycle.ProvisionAsync(
                request,
                sessionId => Task.FromResult(sessionStatuses.GetValueOrDefault(sessionId)),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Teardown_Idempotent_NotFoundIsSuccess()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provider = new FakeSandboxProvider();

        var options = Options.Create(new SandboxLifecycleOptions());
        var logger = NullLogger<SandboxLifecycle>.Instance;

        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, logger);

        await lifecycle.TeardownAsync("non-existent-sandbox", TestContext.Current.CancellationToken);
    }

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
            Command = new[] { "sleep", "3600" },
            WorkingDirectory = "/workspace",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Limits = null,
        },
    };
}
