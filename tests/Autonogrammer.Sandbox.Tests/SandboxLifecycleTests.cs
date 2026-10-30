using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

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
        var provider = new ScriptedSandboxProvider();

        var options = Options.Create(new SandboxLifecycleOptions());
        var logger = NullLogger<SandboxLifecycle>.Instance;

        var lifecycle = new SandboxLifecycle(provider, fakeTime, options, logger);

        await Should.NotThrowAsync(() => lifecycle.TeardownAsync("non-existent-sandbox", TestContext.Current.CancellationToken));

        // A sandbox the lifecycle does not know is a no-op: the provider is never asked to tear anything down.
        provider.TeardownCalls.ShouldBe(0);
        lifecycle.GetAllEntries().ShouldBeEmpty();
    }

    [Fact]
    public async Task StatusTransitions_FollowTheLifecycle()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new ScriptedSandboxProvider();
        var lifecycle = LifecycleTestRequests.CreateLifecycle(provider, new FakeTimeProvider(DateTimeOffset.UtcNow), TimeSpan.FromMinutes(30));
        var request = CreateProvisionRequest();
        provider.OnProvision = (_, _) => Task.FromResult(new ProvisionResult
        {
            Descriptor = new SandboxDescriptor
            {
                Id = "sb-1",
                SessionId = request.SessionId,
                Isolation = request.Isolation,
                Attestation = Attestation.None,
                NativeBinaries = false,
                TtlSeconds = 1800,
                Labels = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            NewlyProvisioned = true,
        });

        // The status the lifecycle records while the provider is inside each teardown attempt. The first attempt fails.
        var statusDuringTeardown = new List<SandboxStatus>();
        provider.OnTeardown = (sandboxId, _) =>
        {
            statusDuringTeardown.Add(lifecycle.GetEntry(sandboxId)!.Status);
            return statusDuringTeardown.Count == 1
                ? throw new TeardownFailedException(sandboxId, 1, "first attempt fails")
                : Task.CompletedTask;
        };

        await lifecycle.ProvisionAsync(request, _ => Task.FromResult<string?>("Running"), ct);
        var entry = lifecycle.GetEntry("sb-1")!;
        entry.Status.ShouldBe(SandboxStatus.Ready);

        await lifecycle.AttachAsync("sb-1", ct);
        entry.Status.ShouldBe(SandboxStatus.Attached);

        await Should.ThrowAsync<TeardownFailedException>(() => lifecycle.TeardownAsync("sb-1", ct));
        entry.Status.ShouldBe(SandboxStatus.Failed);
        lifecycle.GetEntry("sb-1").ShouldBeSameAs(entry);

        await lifecycle.TeardownAsync("sb-1", ct);
        entry.Status.ShouldBe(SandboxStatus.Destroyed);
        lifecycle.GetEntry("sb-1").ShouldBeNull();
        statusDuringTeardown.ShouldBe([SandboxStatus.TearingDown, SandboxStatus.TearingDown]);
    }

    [Fact]
    public void EveryStatusIsEitherReachedByTheLifecycleOrRecordedAsNeverAssigned()
    {
        // The lifecycle assigns these (see StatusTransitions_FollowTheLifecycle); Requested is the registry entry's initial value.
        SandboxStatus[] assigned =
        [
            SandboxStatus.Requested,
            SandboxStatus.Ready,
            SandboxStatus.Attached,
            SandboxStatus.TearingDown,
            SandboxStatus.Destroyed,
            SandboxStatus.Failed,
        ];

        // No code path assigns these today. A new enum value fails this test until it is placed in one of the two groups.
        SandboxStatus[] neverAssigned = [SandboxStatus.Provisioning, SandboxStatus.Degraded];

        Enum.GetValues<SandboxStatus>().Order().ToArray().ShouldBe(assigned.Concat(neverAssigned).Order().ToArray());

        var initial = new SandboxRegistryEntry
        {
            SandboxId = "sb-1",
            SessionId = "session-1",
            IdempotencyKey = "key-1",
            SessionStatusProvider = static _ => Task.FromResult<string?>("Running"),
            ProvisionedAt = DateTimeOffset.UnixEpoch,
            Ttl = TimeSpan.FromMinutes(1),
            Labels = new Dictionary<string, string>(StringComparer.Ordinal),
        };
        initial.Status.ShouldBe(SandboxStatus.Requested);
    }

    [Fact]
    public async Task Provision_TimesOutWhenTheProviderNeverAnswers()
    {
        var provider = new ScriptedSandboxProvider
        {
            OnProvision = static async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                throw new InvalidOperationException("The provision outlived its timeout without being cancelled.");
            },
        };
        var lifecycle = LifecycleTestRequests.CreateLifecycle(provider, new FakeTimeProvider(DateTimeOffset.UtcNow), TimeSpan.FromMinutes(30));
        var request = CreateProvisionRequest() with { Timeout = TimeSpan.FromMilliseconds(50) };

        // The provider waits until it is cancelled, so only the lifecycle's own timeout can end this call. The outer limit makes a missing timeout fail loudly.
        var failure = await Should.ThrowAsync<ProvisionTimeoutException>(
            () => lifecycle.ProvisionAsync(request, _ => Task.FromResult<string?>("Running"), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(30)));

        failure.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
        lifecycle.GetAllEntries().ShouldBeEmpty();
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
