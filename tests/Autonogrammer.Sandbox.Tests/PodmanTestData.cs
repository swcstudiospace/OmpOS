using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Shared request, result and provider builders for LocalPodmanComposeProvider regression tests.
/// </summary>
internal static class PodmanTestData
{
    /// <summary>
    /// Creates a provision request with a unique idempotency key and session.
    /// </summary>
    public static ProvisionRequest CreateRequest() => new()
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
    /// Creates an exec request that runs <c>echo hello</c> in <c>/workspace</c>.
    /// </summary>
    public static ExecRequest CreateExecRequest() => new()
    {
        Argv = ["echo", "hello"],
        WorkingDirectory = "/workspace",
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        Timeout = TimeSpan.FromSeconds(30),
        Tty = false,
    };

    /// <summary>
    /// Creates a successful process result with the given standard output.
    /// </summary>
    public static ProcessResult Ok(string stdout = "") => new()
    {
        ExitCode = 0,
        Stdout = stdout,
        Stderr = string.Empty,
        TimedOut = false,
    };

    /// <summary>
    /// Creates a provider over the given runner, logger and options.
    /// </summary>
    public static LocalPodmanComposeProvider CreateProvider(
        IProcessRunner runner,
        ILogger<LocalPodmanComposeProvider>? logger = null,
        SandboxProviderOptions? options = null)
    {
        return new LocalPodmanComposeProvider(
            runner,
            logger ?? NullLogger<LocalPodmanComposeProvider>.Instance,
            Options.Create(options ?? new SandboxProviderOptions()));
    }

    /// <summary>
    /// Returns whether the spec's argv contains the given argument verbatim.
    /// </summary>
    public static bool HasArgument(ProcessSpec spec, string argument) =>
        spec.Argv.Any(a => string.Equals(a, argument, StringComparison.Ordinal));
}
