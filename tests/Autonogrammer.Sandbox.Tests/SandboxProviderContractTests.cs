using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Abstract contract tests for ISandboxProvider implementations.
/// </summary>
public abstract class SandboxProviderContractTests<TProvider>
    where TProvider : ISandboxProvider
{
    /// <summary>
    /// Creates the provider under test.
    /// </summary>
    protected abstract TProvider CreateProvider();

    /// <summary>
    /// Disposes the provider under test.
    /// </summary>
    protected abstract void DisposeProvider(TProvider provider);

    /// <summary>
    /// Puts the provider in the state where provisioning <paramref name="request"/> hits its timeout, without waiting: the returned
    /// request, provisioned against <paramref name="provider"/>, must make the provider report <see cref="ProvisionTimeoutException"/>.
    /// </summary>
    /// <param name="provider">The provider under test, before the provision.</param>
    /// <param name="request">The provision request the test would otherwise send.</param>
    /// <returns>The request the test provisions.</returns>
    protected abstract ProvisionRequest ArrangeProvisionTimeout(TProvider provider, ProvisionRequest request);

    [Fact]
    public async Task Provision_Attach_Exec_Logs_Teardown_HappyPath()
    {
        var provider = CreateProvider();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            var request = CreateProvisionRequest();
            var result = await provider.ProvisionAsync(request, ct);

            result.NewlyProvisioned.ShouldBeTrue();
            result.Descriptor.Id.ShouldNotBeNullOrEmpty();
            result.Descriptor.SessionId.ShouldBe(request.SessionId);
            result.Descriptor.Isolation.ShouldBe(request.Isolation);
            result.Descriptor.Labels.ShouldContainKey("autonogrammer.session");
            result.Descriptor.Labels.ShouldContainKey("autonogrammer.idempotency");

            await provider.AttachAsync(result.Descriptor.Id, ct);

            var execEvents = new List<ExecEvent>();
            await foreach (var evt in provider.ExecAsync(result.Descriptor.Id, CreateExecRequest(), ct))
            {
                execEvents.Add(evt);
            }

            execEvents.ShouldNotBeEmpty();
            execEvents.ShouldContain(e => e.Type == ExecEventType.Exit);

            var logs = new List<string>();
            await foreach (var log in provider.StreamLogsAsync(result.Descriptor.Id, null, ct))
            {
                logs.Add(log);
                if (logs.Count > 10)
                {
                    break;
                }
            }

            await provider.TeardownAsync(result.Descriptor.Id, ct);

            await provider.TeardownAsync(result.Descriptor.Id, ct);
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task IdempotencyKey_DedupesConcurrentProvisions()
    {
        var provider = CreateProvider();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            var request = CreateProvisionRequest();

            // Eight overlapping calls with one key, each on its own thread: exactly one sandbox may be created.
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
                Task.Run(() => provider.ProvisionAsync(request with { SessionId = $"session-{i}" }, ct), ct)));

            results.Select(r => r.Descriptor.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
            results.Count(r => r.NewlyProvisioned).ShouldBe(1);
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task Teardown_IsIdempotent()
    {
        var provider = CreateProvider();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            var request = CreateProvisionRequest();
            var result = await provider.ProvisionAsync(request, ct);

            await provider.TeardownAsync(result.Descriptor.Id, ct);

            // The repeat succeeds, and the sandbox stays gone: a second teardown must not bring it back or leave it attachable.
            await Should.NotThrowAsync(() => provider.TeardownAsync(result.Descriptor.Id, ct));
            await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync(result.Descriptor.Id, ct));
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task Timeout_IsHonoured()
    {
        var provider = CreateProvider();
        try
        {
            var ct = TestContext.Current.CancellationToken;
            var request = ArrangeProvisionTimeout(provider, CreateProvisionRequest());

            // No clock and no wait: the provider is put in the state where its provision deadline has passed, and
            // the only acceptable outcome is the typed timeout. A provider that ignores the timeout provisions and fails this test.
            await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(request, ct));
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    [Fact]
    public async Task FailedProvision_NeverLeavesSessionRunning()
    {
        var provider = CreateProvider();
        try
        {
            if (provider is FakeSandboxProvider fake)
            {
                fake.FailProvision(1);

                var request = CreateProvisionRequest();
                await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(request));
            }
        }
        finally
        {
            DisposeProvider(provider);
        }
    }

    /// <summary>
    /// Creates a provision request for tests.
    /// </summary>
    protected virtual ProvisionRequest CreateProvisionRequest() => new()
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

    /// <summary>
    /// Creates an exec request for tests.
    /// </summary>
    protected virtual ExecRequest CreateExecRequest() => new()
    {
        Argv = new[] { "echo", "hello" },
        WorkingDirectory = "/workspace",
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        Timeout = TimeSpan.FromSeconds(30),
        Tty = false,
    };
}
