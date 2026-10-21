using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Tests for vendor stub providers - typed failures and capability honesty.
/// </summary>
public sealed class VendorStubTests
{
    [Fact]
    public void OpenShellProvider_Capabilities_AreHonest()
    {
        var runner = new FakeProcessRunner();
        var logger = NullLogger<OpenShellProvider>.Instance;
        var provider = new OpenShellProvider(runner, logger);

        provider.Capabilities.Isolation.ShouldBe(Isolation.Container);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenShellProvider_AllOperations_ThrowNotAvailable()
    {
        var runner = new FakeProcessRunner();
        var logger = NullLogger<OpenShellProvider>.Instance;
        var provider = new OpenShellProvider(runner, logger);
        var request = CreateProvisionRequest();

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));
        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("any"));
        await Should.ThrowAsync<NotAvailableException>(() => provider.ExecAsync("any", CreateExecRequest()).FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.StreamLogsAsync("any").FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.SnapshotAsync("any"));
        await provider.TeardownAsync("any", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OpenShellProvider_CheckPrerequisites_ReturnsFailureForPodman493()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "podman version 4.9.3",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var logger = NullLogger<OpenShellProvider>.Instance;
        var provider = new OpenShellProvider(runner, logger);

        var result = await provider.CheckPrerequisitesAsync(TestContext.Current.CancellationToken);

        result.AllMet.ShouldBeFalse();
        result.FailedPrerequisites.ShouldContain(p => p.Contains("podman", StringComparison.Ordinal) && p.Contains("4.9", StringComparison.Ordinal));
        result.PodmanVersion.ShouldBe("podman version 4.9.3");
    }

    [Fact]
    public void AgentOsProvider_Capabilities_AreHonest()
    {
        var provider = new AgentOsProvider();

        provider.Capabilities.Isolation.ShouldBe(Isolation.Process);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task AgentOsProvider_AllOperations_ThrowNotAvailable()
    {
        var provider = new AgentOsProvider();
        var request = CreateProvisionRequest();

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));
        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("any"));
        await Should.ThrowAsync<NotAvailableException>(() => provider.ExecAsync("any", CreateExecRequest()).FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.StreamLogsAsync("any").FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.SnapshotAsync("any"));
        await provider.TeardownAsync("any", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void RailwaySwarmProvider_Capabilities_AreHonest()
    {
        var provider = new RailwaySwarmProvider();

        provider.Capabilities.Isolation.ShouldBe(Isolation.Container);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task RailwaySwarmProvider_AllOperations_ThrowNotAvailable()
    {
        var provider = new RailwaySwarmProvider();
        var request = CreateProvisionRequest();

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));
        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("any"));
        await Should.ThrowAsync<NotAvailableException>(() => provider.ExecAsync("any", CreateExecRequest()).FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.StreamLogsAsync("any").FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.SnapshotAsync("any"));
        await provider.TeardownAsync("any", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AttestedEnclaveProvider_Capabilities_AreHonest()
    {
        var provider = new AttestedEnclaveProvider();

        provider.Capabilities.Isolation.ShouldBe(Isolation.Process);
        provider.Capabilities.Isolation.ShouldNotBe(Isolation.Enclave);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task AttestedEnclaveProvider_GetQuoteAsync_ReturnsUnverified()
    {
        var provider = new AttestedEnclaveProvider();

        var quote = await provider.GetQuoteAsync("any", new byte[] { 1, 2, 3, 4 }, TestContext.Current.CancellationToken);

        quote.Verdict.ShouldBe(AttestationVerdict.Unverified);
        quote.Quote.ShouldNotBeNull();
    }

    [Fact]
    public async Task AttestedEnclaveProvider_AllOperations_ThrowNotAvailable()
    {
        var provider = new AttestedEnclaveProvider();
        var request = CreateProvisionRequest();

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));
        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("any"));
        await Should.ThrowAsync<NotAvailableException>(() => provider.ExecAsync("any", CreateExecRequest()).FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.StreamLogsAsync("any").FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.SnapshotAsync("any"));
        await provider.TeardownAsync("any", TestContext.Current.CancellationToken);
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

    private static ExecRequest CreateExecRequest() => new()
    {
        Argv = new[] { "echo", "hello" },
        WorkingDirectory = "/workspace",
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        Timeout = TimeSpan.FromSeconds(30),
        Tty = false,
    };
}
