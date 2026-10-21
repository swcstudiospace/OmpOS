using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Serialization;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Canary tests for isolation defaults (SBOX-05).
/// Verifies no host paths/secrets leak into descriptors, logs, or history.
/// </summary>
public sealed class IsolationDefaultsTests
{
    [Fact]
    public async Task FakeProvider_ProvisionedDescriptor_NoHostPathsOrSecrets()
    {
        var provider = new FakeSandboxProvider();
        var request = CreateProvisionRequestWithHostMountAndSecret();

        var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        var descriptorJson = JsonSerializer.Serialize(result.Descriptor, OmpOsJsonContext.Default.SandboxDescriptor);

        descriptorJson.ShouldNotContain("/home/user");
        descriptorJson.ShouldNotContain("/etc/secret");
        descriptorJson.ShouldNotContain("secret-value-123");
        descriptorJson.ShouldNotContain("api-key-456");

        result.Descriptor.Labels.Values.ShouldAllBe(static v =>
            !v.Contains("/home", StringComparison.Ordinal) &&
            !v.Contains("/etc", StringComparison.Ordinal) &&
            !v.Contains("secret", StringComparison.Ordinal) &&
            !v.Contains("api-key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PodmanProvider_ProvisionedDescriptor_NoHostPathsOrSecrets()
    {
        var fakeRunner = new FakeProcessRunner();
        fakeRunner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "0123456789ab",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var options = Options.Create(new SandboxProviderOptions
        {
            AllowHostMounts = true,
            AllowHostSecrets = true,
            PerSandboxCredentials = true,
            OutboundNetworkDefaultDeny = true,
        });

        var logger = NullLogger<LocalPodmanComposeProvider>.Instance;
        var provider = new LocalPodmanComposeProvider(fakeRunner, logger, options);

        var request = CreateProvisionRequestWithHostMountAndSecret();

        var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        var descriptorJson = JsonSerializer.Serialize(result.Descriptor, OmpOsJsonContext.Default.SandboxDescriptor);

        descriptorJson.ShouldNotContain("/home/user");
        descriptorJson.ShouldNotContain("/etc/secret");
        descriptorJson.ShouldNotContain("secret-value-123");
        descriptorJson.ShouldNotContain("api-key-456");

        var upSpec = fakeRunner.Specs.FirstOrDefault(static s => s.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal)));
        upSpec.ShouldNotBeNull();
        var composeContent = upSpec!.Argv.LastOrDefault(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
        composeContent.ShouldNotBeNull();
    }

    [Fact]
    public async Task PodmanProvider_HostMountRejected_WhenAllowHostMountsFalse()
    {
        var fakeRunner = new FakeProcessRunner();
        var options = Options.Create(new SandboxProviderOptions
        {
            AllowHostMounts = false,
        });
        var logger = NullLogger<LocalPodmanComposeProvider>.Instance;
        var provider = new LocalPodmanComposeProvider(fakeRunner, logger, options);

        var request = CreateProvisionRequestWithHostMountAndSecret();

        await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request));
    }

    [Fact]
    public async Task PodmanProvider_HostMountAllowed_WhenAllowHostMountsTrue()
    {
        var fakeRunner = new FakeProcessRunner();
        fakeRunner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "0123456789ab",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var options = Options.Create(new SandboxProviderOptions
        {
            AllowHostMounts = true,
            AllowHostSecrets = true,
        });
        var logger = NullLogger<LocalPodmanComposeProvider>.Instance;
        var provider = new LocalPodmanComposeProvider(fakeRunner, logger, options);

        var request = CreateProvisionRequestWithHostMountAndSecret();

        var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        result.NewlyProvisioned.ShouldBeTrue();
    }

    [Fact]
    public async Task PodmanProvider_BindMountWithAbsoluteSource_RejectedAsHostMount()
    {
        var fakeRunner = new FakeProcessRunner();
        var provider = CreateProviderWithHostMountsDenied(fakeRunner);

        var request = CreateProvisionRequestWithMounts(new MountSpec
        {
            Source = "/home/user/.ssh",
            Target = "/root/.ssh",
            ReadOnly = true,
            Type = MountType.Bind,
        });

        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request));
        exception.Policy.ShouldBe("host-mounts");
        fakeRunner.Specs.ShouldBeEmpty();
    }

    [Fact]
    public async Task PodmanProvider_VolumeMountWithAbsoluteSource_RejectedAsHostMount()
    {
        var fakeRunner = new FakeProcessRunner();
        var provider = CreateProviderWithHostMountsDenied(fakeRunner);

        var request = CreateProvisionRequestWithMounts(new MountSpec
        {
            Source = "/etc",
            Target = "/etc-ro",
            ReadOnly = true,
            Type = MountType.Volume,
        });

        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request));
        exception.Policy.ShouldBe("host-mounts");
        fakeRunner.Specs.ShouldBeEmpty();
    }

    [Fact]
    public async Task PodmanProvider_TmpfsMountWithAbsoluteSource_RejectedAsHostMount()
    {
        var fakeRunner = new FakeProcessRunner();
        var provider = CreateProviderWithHostMountsDenied(fakeRunner);

        var request = CreateProvisionRequestWithMounts(new MountSpec
        {
            Source = "/run/cache",
            Target = "/cache",
            ReadOnly = false,
            Type = MountType.Tmpfs,
        });

        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request));
        exception.Policy.ShouldBe("host-mounts");
        fakeRunner.Specs.ShouldBeEmpty();
    }

    [Fact]
    public async Task PodmanProvider_NamedVolumeWithoutPathCharacters_AllowedWhenHostMountsDenied()
    {
        var fakeRunner = new FakeProcessRunner();
        fakeRunner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "0123456789ab",
            Stderr = string.Empty,
            TimedOut = false,
        });
        var provider = CreateProviderWithHostMountsDenied(fakeRunner);

        var request = CreateProvisionRequestWithMounts(new MountSpec
        {
            Source = "workspace-volume",
            Target = "/workspace",
            ReadOnly = false,
            Type = MountType.Volume,
        });

        var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        result.NewlyProvisioned.ShouldBeTrue();
    }

    private static LocalPodmanComposeProvider CreateProviderWithHostMountsDenied(FakeProcessRunner runner)
    {
        var options = Options.Create(new SandboxProviderOptions
        {
            AllowHostMounts = false,
        });
        var logger = NullLogger<LocalPodmanComposeProvider>.Instance;
        return new LocalPodmanComposeProvider(runner, logger, options);
    }

    private static ProvisionRequest CreateProvisionRequestWithMounts(params MountSpec[] mounts) => new()
    {
        IdempotencyKey = $"test-key-{Guid.NewGuid():N}",
        SessionId = $"session-{Guid.NewGuid():N}",
        Timeout = TimeSpan.FromMinutes(5),
        Isolation = Isolation.Container,
        NetworkPolicy = NetworkPolicy.DefaultDeny,
        Mounts = mounts,
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

    [Fact]
    public async Task NetworkPolicy_DefaultDeny_Enforced()
    {
        var fakeRunner = new FakeProcessRunner();
        fakeRunner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "0123456789ab",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var options = Options.Create(new SandboxProviderOptions
        {
            OutboundNetworkDefaultDeny = true,
        });
        var logger = NullLogger<LocalPodmanComposeProvider>.Instance;
        var provider = new LocalPodmanComposeProvider(fakeRunner, logger, options);

        var request = CreateProvisionRequest() with
        {
            NetworkPolicy = new NetworkPolicy
            {
                OutboundDefault = OutboundDefault.Allow,
                Rules = Array.Empty<NetworkRule>(),
            },
        };

        await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request));
    }

    [Fact]
    public async Task FakeProvider_Logs_NoSecretValues()
    {
        var provider = new FakeSandboxProvider();
        var request = CreateProvisionRequestWithHostMountAndSecret();

        var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        var ct = TestContext.Current.CancellationToken;
        var logs = new List<string>();
        await foreach (var log in provider.StreamLogsAsync(result.Descriptor.Id, null, ct))
        {
            logs.Add(log);
            if (logs.Count > 10)
            {
                break;
            }
        }

        var allLogs = string.Join("\n", logs);
        allLogs.ShouldNotContain("secret-value-123");
        allLogs.ShouldNotContain("api-key-456");
        allLogs.ShouldNotContain("/home/user/.ssh");
    }

    [Fact]
    public async Task PodmanProvider_Exec_NoSecretValuesInOutput()
    {
        var fakeRunner = new FakeProcessRunner();
        fakeRunner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "0123456789ab",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var options = Options.Create(new SandboxProviderOptions
        {
            AllowHostMounts = true,
            AllowHostSecrets = true,
        });
        var logger = NullLogger<LocalPodmanComposeProvider>.Instance;
        var provider = new LocalPodmanComposeProvider(fakeRunner, logger, options);

        var request = CreateProvisionRequestWithHostMountAndSecret();
        var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        fakeRunner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "output\n",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var ct = TestContext.Current.CancellationToken;
        var events = new List<ExecEvent>();
        await foreach (var evt in provider.ExecAsync(result.Descriptor.Id, CreateExecRequest(), ct))
        {
            events.Add(evt);
        }

        var allOutput = string.Join("\n", events.Select(static e => e.Stdout ?? e.Stderr ?? string.Empty));
        allOutput.ShouldNotContain("secret-value-123");
        allOutput.ShouldNotContain("api-key-456");
    }

    private static ProvisionRequest CreateProvisionRequestWithHostMountAndSecret() => new()
    {
        IdempotencyKey = $"test-key-{Guid.NewGuid():N}",
        SessionId = $"session-{Guid.NewGuid():N}",
        Timeout = TimeSpan.FromMinutes(5),
        Isolation = Isolation.Container,
        NetworkPolicy = NetworkPolicy.DefaultDeny,
        Mounts = new[]
        {
            new MountSpec
            {
                Source = "/home/user/.ssh",
                Target = "/root/.ssh",
                ReadOnly = true,
                Type = MountType.Bind,
            },
            new MountSpec
            {
                Source = "workspace-volume",
                Target = "/workspace",
                ReadOnly = false,
                Type = MountType.Volume,
            },
        },
        SecretRefs = new[]
        {
            new SecretRef
            {
                Key = "github-token",
                ProviderSecretId = "secret-value-123",
                TargetEnvVar = "GITHUB_TOKEN",
            },
            new SecretRef
            {
                Key = "api-key",
                ProviderSecretId = "api-key-456",
                TargetEnvVar = "API_KEY",
            },
        },
        Spec = new SandboxSpec
        {
            Image = "alpine:latest",
            Command = new[] { "sleep", "3600" },
            WorkingDirectory = "/workspace",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Limits = null,
        },
    };

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
