using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Sandbox;

/// <summary>
/// OpenShell sandbox provider (stub - not available).
/// Open question: Q-SB-OPENSHELL - What is the openshell sandbox CLI surface (create, exec, logs)?
/// </summary>
public sealed class OpenShellProvider : ISandboxProvider
{
    private const string NotAvailableReason = "openshell provider not implemented";

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<OpenShellProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenShellProvider"/> class.
    /// </summary>
    public OpenShellProvider(IProcessRunner processRunner, ILogger<OpenShellProvider> logger)
    {
        _processRunner = processRunner;
        _logger = logger;

        Capabilities = new SandboxCapability
        {
            Isolation = Isolation.Container,
            Attestation = Attestation.None,
            NativeBinaries = false,
            SnapshotsSupported = false,
            MaxTtlSeconds = 86400,
        };
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; }

    /// <inheritdoc />
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        throw new NotAvailableException(NotAvailableReason);
    }

    /// <inheritdoc />
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        // Idempotent: NotFound is success for a stub that never provisions
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks host prerequisites for OpenShell.
    /// </summary>
    public async Task<PrerequisiteCheckResult> CheckPrerequisitesAsync(CancellationToken cancellationToken = default)
    {
        var failed = new List<string>();
        var podmanVersion = await CheckPodmanVersionAsync(failed, cancellationToken).ConfigureAwait(false);
        var cgroupsV2 = await CheckCgroupsV2Async(failed, cancellationToken).ConfigureAwait(false);
        var userSocketAccessible = CheckUserSocket(failed);

        return failed.Count == 0
            ? PrerequisiteCheckResult.Success(podmanVersion ?? "unknown")
            : PrerequisiteCheckResult.Failure(failed, podmanVersion, cgroupsV2, userSocketAccessible);
    }

    /// <summary>
    /// Generates the OpenShell gateway TOML configuration.
    /// </summary>
    public static string GenerateGatewayToml()
    {
        var config = new OpenShellGatewayConfig();
        // TOML serialization would use a proper TOML library
        // This is a simplified representation for golden testing
        return $"""
[openshell]
version = {config.Version}

[openshell.gateway]
compute_driver = "{config.Gateway.ComputeDriver}"
grpc_endpoint = "{config.Gateway.GrpcEndpoint}"

[openshell.drivers.podman]
socket_path = "{config.Drivers.Podman.SocketPath}"
network_name = "{config.Drivers.Podman.NetworkName}"
sandbox_runtime_image = "{config.Drivers.Podman.SandboxRuntimeImage}"
supervisor_image = "{config.Drivers.Podman.SupervisorImage}"
image_pull_policy = "{config.Drivers.Podman.ImagePullPolicy}"
stop_timeout_secs = {config.Drivers.Podman.StopTimeoutSecs}
grpc_endpoint = "{config.Drivers.Podman.GrpcEndpoint}"
""";
    }

    /// <summary>
    /// Generates the OpenShell policy YAML configuration.
    /// </summary>
    /// <param name="policy">The policy to render. Defaults to the safe-default policy when omitted.</param>
    /// <remarks>
    /// Every string interpolated into the document is rendered through <c>NetworkPolicyRule.ToYamlScalar</c>,
    /// so values containing YAML syntax are quoted and escaped rather than spliced in verbatim.
    /// </remarks>
    public static string GeneratePolicyYaml(OpenShellPolicy? policy = null)
    {
        policy ??= new OpenShellPolicy();
        // YAML serialization would use YamlDotNet
        // This is a simplified representation for golden testing
        return $"""
version: {policy.Version}
filesystem_policy:
  include_workdir: {(policy.FilesystemPolicy.IncludeWorkdir ? "true" : "false")}
  read_only: {FormatYamlSequence(policy.FilesystemPolicy.ReadOnly.Select(ToScalar))}
  read_write: {FormatYamlSequence(policy.FilesystemPolicy.ReadWrite.Select(ToScalar))}
landlock:
  compatibility: {ToScalar(policy.Landlock.Compatibility)}
network_policies: {FormatYamlSequence(policy.NetworkPolicies.Select(static rule => rule?.ToString() ?? string.Empty))}
""";
    }

    private static string ToScalar(string? value) => NetworkPolicyRule.ToYamlScalar(value ?? string.Empty);

    private static string FormatYamlSequence(IEnumerable<string> renderedItems)
    {
        var items = renderedItems.ToList();
        if (items.Count == 0)
        {
            return "[]";
        }

        return "\n  - " + string.Join("\n  - ", items);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Prerequisite probing must never throw; any probe failure is recorded as a failed prerequisite.")]
    private async Task<string?> CheckPodmanVersionAsync(List<string> failed, CancellationToken cancellationToken)
    {
        string? podmanVersion = null;
        try
        {
            var result = await _processRunner.RunAsync(new ProcessSpec
            {
                Argv = ["podman", "--version"],
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Timeout = TimeSpan.FromSeconds(10),
            }, cancellationToken).ConfigureAwait(false);

            if (result.ExitCode == 0)
            {
                podmanVersion = result.Stdout.Trim();
                var parts = podmanVersion.Split(' ');
                if (parts.Length >= 3 && Version.TryParse(parts[2], out var version))
                {
                    if (version.Major < 5)
                    {
                        failed.Add($"podman version {version} < 5.0.0 (requires podman 5.x)");
                    }
                }
            }
            else
            {
                failed.Add("podman not found");
            }
        }
        catch
        {
            failed.Add("podman not accessible");
        }

        return podmanVersion;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Prerequisite probing must never throw; any probe failure is recorded as a failed prerequisite.")]
    private static async Task<bool> CheckCgroupsV2Async(List<string> failed, CancellationToken cancellationToken)
    {
        try
        {
            var cgroupContent = await File.ReadAllTextAsync("/proc/self/cgroup", cancellationToken).ConfigureAwait(false);
            var cgroupsV2 = cgroupContent.Contains("0::/", StringComparison.Ordinal)
                || cgroupContent.Contains("cgroup2", StringComparison.Ordinal);
            if (!cgroupsV2)
            {
                failed.Add("cgroups v2 not enabled");
            }

            return cgroupsV2;
        }
        catch
        {
            failed.Add("cannot check cgroups");
            return false;
        }
    }

    private static bool CheckUserSocket(List<string> failed)
    {
        var socketPath = $"/run/user/{Environment.GetEnvironmentVariable("UID") ?? "1000"}/podman/podman.sock";
        var userSocketAccessible = File.Exists(socketPath);
        if (!userSocketAccessible)
        {
            failed.Add($"user podman socket not found at {socketPath}");
        }

        return userSocketAccessible;
    }
}
