using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Regression tests for provision failure cleanup (CR-01) and provision input validation (WR-01, WR-02, WR-03).
/// </summary>
public sealed class ProvisionCleanupRegressionTests
{
    private static LocalPodmanComposeProvider CreateProvider(FakeProcessRunner runner, SandboxProviderOptions? options = null)
    {
        return new LocalPodmanComposeProvider(
            runner,
            NullLogger<LocalPodmanComposeProvider>.Instance,
            Options.Create(options ?? new SandboxProviderOptions()));
    }

    private static ProvisionRequest CreateRequest() => new()
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

    private static ProcessResult Ok(string stdout = "")
    {
        return new ProcessResult
        {
            ExitCode = 0,
            Stdout = stdout,
            Stderr = string.Empty,
            TimedOut = false,
        };
    }

    /// <summary>
    /// A typed up failure still deletes the compose file and attempts partial teardown (CR-01).
    /// </summary>
    [Fact]
    public async Task TypedUpFailure_DeletesComposeFileAndAttemptsDown()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            PodmanTestData.Ok(),
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false },
            Ok());
        var provider = CreateProvider(runner);

        var ct = TestContext.Current.CancellationToken;
        var exception = await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(CreateRequest(), ct));

        // Rethrown untouched: the typed up message, not the generic provision wrapper.
        exception.Message.ShouldContain("podman-compose up failed");
        runner.Specs.ShouldContain(static s => s.Argv.Any(static a => string.Equals(a, "down", StringComparison.Ordinal)));

        var upSpec = runner.Specs.First(s => s.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal)));
        var composeFile = upSpec.Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
        File.Exists(composeFile).ShouldBeFalse();
    }

    /// <summary>
    /// A generic runner exception during up still deletes the compose file and attempts teardown with down (P3-04).
    /// </summary>
    [Fact]
    public async Task GenericRunnerFailure_DeletesComposeFileAndAttemptsDown()
    {
        var runner = new FakeProcessRunner();
        runner.ThrowForSpec = static spec => spec.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal))
            ? new InvalidOperationException("runner blew up")
            : null;
        var provider = CreateProvider(runner);

        var ct = TestContext.Current.CancellationToken;
        var exception = await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(CreateRequest(), ct));

        exception.Message.ShouldContain("Provisioning failed");
        runner.Specs.ShouldContain(static s => s.Argv.Any(static a => string.Equals(a, "down", StringComparison.Ordinal)));

        var upSpec = runner.Specs.First(s => s.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal)));
        var composeFile = upSpec.Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
        File.Exists(composeFile).ShouldBeFalse();
    }

    /// <summary>
    /// An empty container lookup fails closed with cleanup instead of registering a garbage id (CR-01, WR-01).
    /// </summary>
    [Fact]
    public async Task EmptyLookupResult_DeletesComposeFileAndAttemptsDown()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(PodmanTestData.Ok(), Ok(), Ok(string.Empty), Ok());
        var provider = CreateProvider(runner);

        var ct = TestContext.Current.CancellationToken;
        var exception = await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(CreateRequest(), ct));

        exception.Message.ShouldContain("expected exactly one");
        runner.Specs.ShouldContain(static s => s.Argv.Any(static a => string.Equals(a, "down", StringComparison.Ordinal)));

        var upSpec = runner.Specs.First(s => s.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal)));
        var composeFile = upSpec.Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
        File.Exists(composeFile).ShouldBeFalse();
    }

    /// <summary>
    /// Multiple lookup rows fail closed instead of trusting an embedded-newline blob (WR-01).
    /// </summary>
    [Fact]
    public async Task MultiLineLookupResult_FailsClosed()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(PodmanTestData.Ok(), Ok(), Ok("abc123\ndef456\n"), Ok());
        var provider = CreateProvider(runner);

        var ct = TestContext.Current.CancellationToken;
        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(CreateRequest(), ct));
    }

    /// <summary>
    /// A SessionId with podman filter syntax is rejected before any process runs (WR-02).
    /// </summary>
    [Fact]
    public async Task FilterInjectionSessionId_Rejected()
    {
        var runner = new FakeProcessRunner();
        var provider = CreateProvider(runner);

        var ct = TestContext.Current.CancellationToken;
        var request = CreateRequest() with { SessionId = "victim,other=x" };
        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request, ct));

        exception.Policy.ShouldBe("session-id");
        runner.Specs.ShouldBeEmpty();
    }

    /// <summary>
    /// Secret references are rejected when host secrets are disallowed (WR-03).
    /// </summary>
    [Fact]
    public async Task SecretRefs_RejectedWhenHostSecretsDisallowed()
    {
        var runner = new FakeProcessRunner();
        var provider = CreateProvider(runner, new SandboxProviderOptions { AllowHostSecrets = false });

        var ct = TestContext.Current.CancellationToken;
        var request = CreateRequest() with
        {
            SecretRefs = [new SecretRef { Key = "k", ProviderSecretId = "s", TargetEnvVar = "T" }],
        };
        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request, ct));

        exception.Policy.ShouldBe("host-secrets");
        runner.Specs.ShouldBeEmpty();
    }

    /// <summary>
    /// Duplicate secret targets are rejected when per-sandbox credentials are required (WR-03).
    /// </summary>
    [Fact]
    public async Task DuplicateSecretTarget_RejectedWhenPerSandboxCredentials()
    {
        var runner = new FakeProcessRunner();
        var provider = CreateProvider(runner, new SandboxProviderOptions { AllowHostSecrets = true, PerSandboxCredentials = true });

        var ct = TestContext.Current.CancellationToken;
        var request = CreateRequest() with
        {
            SecretRefs =
            [
                new SecretRef { Key = "a", ProviderSecretId = "s1", TargetEnvVar = "SAME" },
                new SecretRef { Key = "b", ProviderSecretId = "s2", TargetEnvVar = "SAME" },
            ],
        };
        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request, ct));

        exception.Policy.ShouldBe("per-sandbox-credentials");
        runner.Specs.ShouldBeEmpty();
    }
}
