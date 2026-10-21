using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;

namespace OmpOS.Sandbox;

/// <summary>
/// Local podman-compose sandbox provider.
/// Generates per-sandbox compose files and manages them via podman-compose.
/// </summary>
public sealed class LocalPodmanComposeProvider : ISandboxProvider
{
    private static readonly Action<ILogger, string, Exception?> ComposeCleanupFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, "ComposeCleanupFailed"),
            "Failed to delete compose file {ComposeFile} during teardown");
    private static readonly Action<ILogger, string, int, Exception?> ComposeUpFailed =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(2, "ComposeUpFailed"),
            "podman-compose up failed for project {ProjectName} (exit {ExitCode})");
    private static readonly Action<ILogger, string, string, Exception?> PartialTeardownFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(3, "PartialTeardownFailed"),
            "Best-effort partial teardown failed for project {ProjectName} ({Reason}); containers labelled with the ompos.project label may remain");

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<LocalPodmanComposeProvider> _logger;
    private readonly SandboxProviderOptions _options;
    private readonly ConcurrentDictionary<string, PodmanSandbox> _sandboxes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<PodmanSandbox>> _provisionsByKey = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalPodmanComposeProvider"/> class.
    /// </summary>
    public LocalPodmanComposeProvider(
        IProcessRunner processRunner,
        ILogger<LocalPodmanComposeProvider> logger,
        IOptions<SandboxProviderOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _processRunner = processRunner;
        _logger = logger;
        _options = options.Value;

        Capabilities = new SandboxCapability
        {
            Isolation = Isolation.Container,
            Attestation = Attestation.None,
            NativeBinaries = false,
            SnapshotsSupported = false,
            MaxTtlSeconds = _options.MaxTtlMinutes * 60L,
        };
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; }

    /// <inheritdoc />
    public async Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequestIds(request);
        ValidateIsolationDefaults(request);

        // The key is reserved before the first await, so overlapping calls with one IdempotencyKey either
        // become the owner or await the owner's single outcome. Only the owner reports NewlyProvisioned.
        var reservation = new TaskCompletionSource<PodmanSandbox>(TaskCreationOptions.RunContinuationsAsynchronously);
        while (!_provisionsByKey.TryAdd(request.IdempotencyKey, reservation.Task))
        {
            if (_provisionsByKey.TryGetValue(request.IdempotencyKey, out var inFlight))
            {
                var shared = await inFlight.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new ProvisionResult
                {
                    Descriptor = shared.Descriptor,
                    NewlyProvisioned = false,
                };
            }
        }

        PodmanSandbox sandbox;
        try
        {
            sandbox = await CreateSandboxAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Release the key so a retry can provision again, and hand the same failure to every waiter.
            _provisionsByKey.TryRemove(request.IdempotencyKey, out _);
            reservation.SetException(ex);
            throw;
        }

        reservation.SetResult(sandbox);
        return new ProvisionResult
        {
            Descriptor = sandbox.Descriptor,
            NewlyProvisioned = true,
        };
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Provisioning must clean up the compose file on any failure and surface a single typed ProvisionTimeoutException; typed SandboxExceptions pass through untouched.")]
    private async Task<PodmanSandbox> CreateSandboxAsync(ProvisionRequest request, CancellationToken cancellationToken)
    {
        var sandboxId = $"podman-{Guid.NewGuid().ToString("N")[..12]}";
        var projectName = $"ompos-{sandboxId}";
        var composeFile = Path.Combine(Path.GetTempPath(), $"{projectName}.yaml");

        try
        {
            return await ProvisionCoreAsync(sandboxId, projectName, composeFile, request, cancellationToken).ConfigureAwait(false);
        }
        catch (SandboxException)
        {
            // Typed failures (non-zero up exit, timeout, container lookup) are the dominant
            // provision failure modes: tear down any half-started containers best-effort,
            // delete the compose file, and rethrow untouched so callers see the typed error.
            await TeardownPartialAsync(projectName, composeFile).ConfigureAwait(false);
            DeleteComposeFile(composeFile);
            throw;
        }
        catch (Exception ex)
        {
            await TeardownPartialAsync(projectName, composeFile).ConfigureAwait(false);
            DeleteComposeFile(composeFile);
            throw new ProvisionTimeoutException($"Provisioning failed: {ex.Message}", ex);
        }
    }

    private async Task<PodmanSandbox> ProvisionCoreAsync(string sandboxId, string projectName, string composeFile, ProvisionRequest request, CancellationToken cancellationToken)
    {
        await WriteComposeFileAsync(composeFile, sandboxId, projectName, request, cancellationToken).ConfigureAwait(false);
        await BringUpAsync(projectName, composeFile, request.Timeout, cancellationToken).ConfigureAwait(false);
        var containerId = await LookupContainerIdAsync(request.SessionId, sandboxId, cancellationToken).ConfigureAwait(false);

        var sandbox = new PodmanSandbox
        {
            Id = sandboxId,
            IdempotencyKey = request.IdempotencyKey,
            SessionId = request.SessionId,
            Descriptor = CreateDescriptor(sandboxId, projectName, request),
            ProjectName = projectName,
            ComposeFile = composeFile,
            ContainerId = containerId,
            Spec = request.Spec,
            Status = SandboxStatus.Ready,
        };

        _sandboxes.TryAdd(sandboxId, sandbox);
        return sandbox;
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not found");
        }

        sandbox.Status = SandboxStatus.Attached;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteIteratorAsync(sandboxId, request, cancellationToken);
    }

    private async IAsyncEnumerable<ExecEvent> ExecuteIteratorAsync(string sandboxId, ExecRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not found");
        }

        var argv = new List<string> { "podman", "exec" };
        if (request.Tty)
        {
            argv.Add("-t");
        }

        // Workdir and environment are applied inside the container by podman exec. The host podman
        // process keeps its own environment and working directory.
        if (!string.IsNullOrEmpty(request.WorkingDirectory))
        {
            argv.Add("--workdir");
            argv.Add(request.WorkingDirectory);
        }

        // podman takes the value of a bare NAME from the environment of the podman process itself, so
        // VALUE is passed through ProcessSpec.Environment and never appears in argv (visible to ps).
        var childEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in request.Environment)
        {
            argv.Add("--env");
            argv.Add(name);
            childEnvironment[name] = value;
        }

        argv.Add(sandbox.ContainerId);
        argv.AddRange(request.Argv);

        var result = await _processRunner.RunAsync(new ProcessSpec
        {
            Argv = argv,
            WorkingDirectory = "/",
            Environment = childEnvironment,
            Timeout = request.Timeout,
        }, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(result.Stdout))
        {
            yield return new ExecEvent { Type = ExecEventType.Stdout, Stdout = result.Stdout };
        }

        if (!string.IsNullOrEmpty(result.Stderr))
        {
            yield return new ExecEvent { Type = ExecEventType.Stderr, Stderr = result.Stderr };
        }

        yield return new ExecEvent { Type = ExecEventType.Exit, ExitCode = result.ExitCode };
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        return StreamLogsIteratorAsync(sandboxId, cursor, cancellationToken);
    }

    private async IAsyncEnumerable<string> StreamLogsIteratorAsync(string sandboxId, LogCursor? cursor, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not found");
        }

        var follow = cursor?.Follow ?? false;
        var since = cursor?.Position > 0 ? $"--since={cursor.Position}s" : null;

        var argv = new List<string> { "podman", "logs" };
        if (follow)
        {
            argv.Add("-f");
        }

        if (since is not null)
        {
            argv.Add(since);
        }

        argv.Add(sandbox.ContainerId);

        var spec = new ProcessSpec
        {
            Argv = argv,
            WorkingDirectory = "/",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = follow ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(30),
        };

        if (follow)
        {
            // Lines are yielded as podman writes them; waiting for exit would starve a live follow.
            await foreach (var line in _processRunner.StreamStdoutLinesAsync(spec, cancellationToken).ConfigureAwait(false))
            {
                yield return line;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var result = await _processRunner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
            foreach (var line in result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return line;
            }
        }
    }

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out _))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not found");
        }

        return Task.FromResult(SnapshotResult.NotSupportedResult("Snapshots not supported by LocalPodmanComposeProvider"));
    }

    /// <inheritdoc />
    public async Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            return;
        }

        sandbox.Status = SandboxStatus.TearingDown;

        // A thrown RunAsync propagates with the record (and compose file) intact for retry.
        var result = await _processRunner.RunAsync(new ProcessSpec
        {
            Argv = ["podman-compose", "-p", sandbox.ProjectName, "-f", sandbox.ComposeFile, "down", "-v"],
            WorkingDirectory = Path.GetDirectoryName(sandbox.ComposeFile)!,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = TimeSpan.FromMinutes(5),
        }, cancellationToken).ConfigureAwait(false);

        if (result.TimedOut)
        {
            throw new TeardownFailedException(sandboxId, 1, $"podman-compose down timed out for sandbox {sandboxId}");
        }

        if (result.ExitCode != 0)
        {
            throw new TeardownFailedException(sandboxId, 1, $"podman-compose down failed for sandbox {sandboxId} (exit {result.ExitCode})");
        }

        // Success: local state and registry entry go away together. On failure both are
        // retained so lifecycle retries can re-run down with the same compose file.
        // The key is released only while it still maps to this sandbox: an overlapping
        // teardown may finish first and a new provision may already have re-reserved the
        // key, and an unconditional remove would drop the new reservation and allow a
        // duplicate sandbox for one key. The pair-form TryRemove makes the check atomic.
        DeleteComposeFile(sandbox.ComposeFile);
        _sandboxes.TryRemove(sandboxId, out _);
        if (_provisionsByKey.TryGetValue(sandbox.IdempotencyKey, out var reservation)
            && reservation.IsCompletedSuccessfully
            && string.Equals((await reservation.ConfigureAwait(false)).Id, sandboxId, StringComparison.Ordinal))
        {
            _ = _provisionsByKey.TryRemove(new KeyValuePair<string, Task<PodmanSandbox>>(sandbox.IdempotencyKey, reservation));
        }
        }

    private static SandboxDescriptor CreateDescriptor(string sandboxId, string projectName, ProvisionRequest request)
    {
        return new SandboxDescriptor
        {
            Id = sandboxId,
            SessionId = request.SessionId,
            Isolation = request.Isolation,
            Attestation = Attestation.None,
            NativeBinaries = false,
            TtlSeconds = (long)request.Timeout.TotalSeconds,
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ompos.session"] = request.SessionId,
                ["ompos.sandbox"] = sandboxId,
                ["ompos.idempotency"] = request.IdempotencyKey,
                ["ompos.project"] = projectName,
            },
        };
    }

    private async Task WriteComposeFileAsync(string composeFile, string sandboxId, string projectName, ProvisionRequest request, CancellationToken cancellationToken)
    {
        var composeContent = GenerateComposeFile(sandboxId, projectName, request);
        await File.WriteAllTextAsync(composeFile, composeContent, cancellationToken).ConfigureAwait(false);
    }

    private async Task BringUpAsync(string projectName, string composeFile, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var upResult = await _processRunner.RunAsync(new ProcessSpec
        {
            Argv = ["podman-compose", "-p", projectName, "-f", composeFile, "up", "-d", "--wait"],
            WorkingDirectory = Path.GetDirectoryName(composeFile)!,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = timeout,
        }, cancellationToken).ConfigureAwait(false);

        if (upResult is { ExitCode: not 0, TimedOut: false })
        {
            // Tool output is never embedded: the compose file serializes Spec.Environment
            // verbatim and compose diagnostics echo file fragments that can carry values.
            ComposeUpFailed(_logger, projectName, upResult.ExitCode, null);
            throw new ProvisionTimeoutException($"podman-compose up failed for project {projectName} (exit {upResult.ExitCode})");
        }

        if (upResult.TimedOut)
        {
            throw new ProvisionTimeoutException("podman-compose up timed out");
        }
    }

    private async Task<string> LookupContainerIdAsync(string sessionId, string sandboxId, CancellationToken cancellationToken)
    {
        var psResult = await _processRunner.RunAsync(new ProcessSpec
        {
            Argv = ["podman", "ps", "--filter", $"label=ompos.session={sessionId}", "--filter", $"label=ompos.sandbox={sandboxId}", "--format", "{{.ID}}"],
            WorkingDirectory = "/",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = TimeSpan.FromSeconds(30),
        }, cancellationToken).ConfigureAwait(false);

        if (psResult.TimedOut)
        {
            throw new ProvisionTimeoutException($"Container lookup timed out for sandbox {sandboxId}");
        }

        if (psResult.ExitCode != 0)
        {
            throw new ProvisionTimeoutException($"Container lookup failed for sandbox {sandboxId} (exit {psResult.ExitCode})");
        }

        // SessionId is charset-validated at entry and sandboxId is provider-generated, so the
        // label filters cannot match a foreign container; still require exactly one id so a
        // stale project or label collision fails closed instead of exec'ing the wrong box.
        var ids = psResult.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static id => id.Length > 0)
            .ToList();
        if (ids.Count != 1 || ids[0].Any(char.IsWhiteSpace))
        {
            throw new ProvisionTimeoutException($"Container lookup for sandbox {sandboxId} returned {ids.Count} containers; expected exactly one");
        }

        return ids[0];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Compose-file deletion is best-effort cleanup on failure paths; a cleanup failure must never mask the in-flight provisioning or teardown error.")]
    private void DeleteComposeFile(string composeFile)
    {
        try
        {
            if (File.Exists(composeFile))
            {
                File.Delete(composeFile);
            }
        }
        catch (Exception ex)
        {
            ComposeCleanupFailed(_logger, composeFile, ex);
        }
    }

    private static void ValidateRequestIds(ProvisionRequest request)
    {
        if (!IsSafeId(request.SessionId))
        {
            throw new PolicyDeniedException("session-id", $"Invalid SessionId '{request.SessionId}': allowed characters are letters, digits, '.', '_' and '-' (max 128).");
        }

        if (!IsSafeId(request.IdempotencyKey))
        {
            throw new PolicyDeniedException("idempotency-key", "Invalid IdempotencyKey: allowed characters are letters, digits, '.', '_' and '-' (max 128).");
        }
    }

    private static bool IsSafeId(string value)
    {
        return value.Length > 0
            && value.Length <= 128
            && value.All(static c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-');
    }

    private void ValidateIsolationDefaults(ProvisionRequest request)
    {
        if (!_options.AllowHostMounts)
        {
            foreach (var mount in request.Mounts)
            {
                if (mount.Type == MountType.Bind || IsHostPathLike(mount.Source))
                {
                    throw new PolicyDeniedException("host-mounts", "Host mounts are not allowed (AllowHostMounts=false)");
                }
            }
        }

        if (!_options.AllowHostSecrets && request.SecretRefs.Count > 0)
        {
            throw new PolicyDeniedException("host-secrets", "Secret references are not allowed (AllowHostSecrets=false)");
        }

        if (_options.PerSandboxCredentials)
        {
            var duplicates = request.SecretRefs
                .GroupBy(static s => s.TargetEnvVar, StringComparer.Ordinal)
                .Where(static g => g.Count() > 1)
                .Select(static g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
            {
                throw new PolicyDeniedException("per-sandbox-credentials", $"Duplicate secret target(s) with per-sandbox credentials: {string.Join(", ", duplicates)}");
            }
        }

        if (_options.OutboundNetworkDefaultDeny && request.NetworkPolicy.OutboundDefault == OutboundDefault.Allow)
        {
            throw new PolicyDeniedException("network-default-deny", "Outbound default allow requires explicit configuration");
        }
    }

    /// <summary>
    /// Determines whether a mount source is host-path-like and therefore denied when host mounts are disabled.
    /// Plain named volumes (no path characters) are allowed; anything rooted or containing a
    /// path separator is treated as a host path regardless of <see cref="MountType"/>.
    /// </summary>
    /// <param name="source">The mount source to inspect.</param>
    /// <returns>True when the source looks like a host path.</returns>
    private static bool IsHostPathLike(string source)
    {
        return source.Length == 0
            || source[0] == '/'
            || source[0] == '~'
            || source[0] == '.'
            || source.Contains('/', StringComparison.Ordinal)
            || source.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(source);
    }

    /// <summary>
    /// Tears down a partially provisioned sandbox (compose file may exist, registry entry does not).
    /// </summary>
    /// <param name="projectName">The compose project name.</param>
    /// <param name="composeFile">The generated compose file path.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Partial teardown is best-effort cleanup on a failure path; any failure must be logged, never mask the in-flight provisioning error.")]
    private async Task TeardownPartialAsync(string projectName, string composeFile)
    {
        if (!File.Exists(composeFile))
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await _processRunner.RunAsync(new ProcessSpec
            {
                Argv = ["podman-compose", "-p", projectName, "-f", composeFile, "down", "-v"],
                WorkingDirectory = Path.GetDirectoryName(composeFile) ?? "/",
                Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                Timeout = TimeSpan.FromMinutes(2),
            }, cts.Token).ConfigureAwait(false);

            if (result.TimedOut)
            {
                PartialTeardownFailed(_logger, projectName, "timed out", null);
            }
            else if (result.ExitCode != 0)
            {
                PartialTeardownFailed(_logger, projectName, $"exit {result.ExitCode.ToString(CultureInfo.InvariantCulture)}", null);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: never mask the in-flight provisioning failure, including on
            // cancellation. The compose file is still deleted by the caller.
            PartialTeardownFailed(_logger, projectName, "runner failed", ex);
        }
    }

    private string GenerateComposeFile(string sandboxId, string projectName, ProvisionRequest request)
    {
        var document = new ComposeDocument();
        var service = new ComposeService
        {
            Image = request.Spec.Image,
            Command = request.Spec.Command.Count > 0 ? request.Spec.Command : null,
            WorkingDirectory = request.Spec.WorkingDirectory,
            Environment = request.Spec.Environment,
            NetworkMode = request.NetworkPolicy.OutboundDefault == OutboundDefault.Deny ? "none" : "bridge",
            ReadOnly = true,
            PidsLimit = request.Spec.Limits?.PidsLimit ?? 100,
            MemoryLimit = request.Spec.Limits?.MemoryBytes is long memoryBytes
                ? memoryBytes.ToString(CultureInfo.InvariantCulture)
                : null,
            Cpus = request.Spec.Limits?.CpuCores is double cpuCores
                ? cpuCores.ToString("F2", CultureInfo.InvariantCulture)
                : null,
        };
        service.Labels["ompos.session"] = request.SessionId;
        service.Labels["ompos.sandbox"] = sandboxId;
        service.Labels["ompos.project"] = projectName;
        foreach (var mount in request.Mounts.Where(m => m.Type != MountType.Bind || _options.AllowHostMounts))
        {
            service.Volumes.Add(new ComposeVolume
            {
                Source = mount.Source,
                Target = mount.Target,
                ReadOnly = mount.ReadOnly,
                Type = MountTypeName(mount.Type),
            });
        }

        document.Services["sandbox"] = service;
        document.Networks["default"] = request.NetworkPolicy.OutboundDefault == OutboundDefault.Deny
            ? new ComposeNetwork { Internal = true, Driver = "bridge" }
            : new ComposeNetwork { Driver = "bridge" };

        return JsonSerializer.Serialize(document, SandboxJsonContext.Default.ComposeDocument);
    }

    private static string MountTypeName(MountType type) => type switch
    {
        MountType.Bind => "bind",
        MountType.Volume => "volume",
        MountType.Tmpfs => "tmpfs",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown mount type."),
    };

    private sealed class PodmanSandbox
    {
        public required string Id { get; init; }

        public required string IdempotencyKey { get; init; }

        public required string SessionId { get; init; }

        public required SandboxDescriptor Descriptor { get; init; }

        public required string ProjectName { get; init; }

        public required string ComposeFile { get; init; }

        public required string ContainerId { get; init; }

        public required SandboxSpec Spec { get; init; }

        public SandboxStatus Status { get; set; }
    }
}
