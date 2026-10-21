using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests for provider teardown failure reporting (CR-02).
/// </summary>
public sealed class TeardownFailureTests
{
    private static (LocalPodmanComposeProvider Provider, FakeProcessRunner Runner) CreateProvider()
    {
        var runner = new FakeProcessRunner();
        var provider = new LocalPodmanComposeProvider(
            runner,
            NullLogger<LocalPodmanComposeProvider>.Instance,
            Options.Create(new SandboxProviderOptions()));
        return (provider, runner);
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

    private static async Task<string> ProvisionAsync(LocalPodmanComposeProvider provider, CancellationToken ct)
    {
        var result = await provider.ProvisionAsync(CreateRequest(), ct).ConfigureAwait(false);
        return result.Descriptor.Id;
    }

    /// <summary>
    /// A non-zero down exit throws a typed exception without raw stderr, and the compose file is retained for retry (CR-02, WR-05).
    /// </summary>
    [Fact]
    public async Task NonZeroDownExit_ThrowsTypedWithoutRawStderr()
    {
        var (provider, runner) = CreateProvider();
        runner.EnqueueResults(Ok(), Ok("container-id-123"));
        runner.SetResult(new ProcessResult { ExitCode = 3, Stdout = string.Empty, Stderr = "leak-marker-xyz", TimedOut = false });

        var ct = TestContext.Current.CancellationToken;
        var sandboxId = await ProvisionAsync(provider, ct);

        var exception = await Should.ThrowAsync<TeardownFailedException>(() => provider.TeardownAsync(sandboxId, ct));

        exception.SandboxId.ShouldBe(sandboxId);
        exception.Message.ShouldContain("(exit 3)");
        exception.Message.ShouldNotContain("leak-marker-xyz");

        // Retained so the lifecycle retry can re-run down with the same compose file.
        var upSpec = runner.Specs.First(s => s.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal)));
        var composeFile = upSpec.Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
        File.Exists(composeFile).ShouldBeTrue();

        // Best-effort cleanup of the retained file so the test leaves nothing behind.
        File.Delete(composeFile);
    }

    /// <summary>
    /// A timed-out down throws a typed exception so the lifecycle retry can fire (CR-02).
    /// </summary>
    [Fact]
    public async Task TimedOutDown_ThrowsTyped()
    {
        var (provider, runner) = CreateProvider();
        runner.EnqueueResults(Ok(), Ok("container-id-123"));
        runner.SetResult(new ProcessResult { ExitCode = 0, Stdout = string.Empty, Stderr = string.Empty, TimedOut = true });

        var ct = TestContext.Current.CancellationToken;
        var sandboxId = await ProvisionAsync(provider, ct);

        var exception = await Should.ThrowAsync<TeardownFailedException>(() => provider.TeardownAsync(sandboxId, ct));

        exception.Message.ShouldContain("timed out");

        var upSpec = runner.Specs.First(s => s.Argv.Any(static a => string.Equals(a, "up", StringComparison.Ordinal)));
        var composeFile = upSpec.Argv.First(static a => a.EndsWith(".yaml", StringComparison.Ordinal));
        if (File.Exists(composeFile))
        {
            File.Delete(composeFile);
        }
    }
}
