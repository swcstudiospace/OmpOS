using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;

namespace OmpOS.Sandbox;

/// <summary>
/// Deterministic in-memory sandbox provider for testing.
/// Supports configurable failure injection.
/// </summary>
public sealed class FakeSandboxProvider : ISandboxProvider
{
    private readonly ConcurrentDictionary<string, FakeSandbox> _sandboxes = new(StringComparer.Ordinal);
    private readonly FakeSandboxProviderOptions _options;
    private int _provisionFailCount;
    private int _teardownFailCount;
    private readonly object _provisionLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeSandboxProvider"/> class.
    /// </summary>
    /// <param name="options">Optional configuration options.</param>
    public FakeSandboxProvider(FakeSandboxProviderOptions? options = null)
    {
        _options = options ?? new FakeSandboxProviderOptions();
        Capabilities = new SandboxCapability
        {
            Isolation = Isolation.Process,
            Attestation = Attestation.None,
            NativeBinaries = false,
            SnapshotsSupported = false,
            MaxTtlSeconds = 86400,
        };
    }

    /// <inheritdoc />
    public SandboxCapability Capabilities { get; }

    /// <summary>
    /// Configures the provider to fail provisioning N times.
    /// </summary>
    /// <param name="count">Number of times to fail.</param>
    public void FailProvision(int count)
    {
        _provisionFailCount = count;
    }

    /// <summary>
    /// Configures the provider to fail teardown M times.
    /// </summary>
    /// <param name="count">Number of times to fail.</param>
    public void FailTeardown(int count)
    {
        _teardownFailCount = count;
    }

    /// <summary>
    /// Configures the provider to fail after partial provisioning.
    /// </summary>
    /// <param name="failAfterPartial">Whether to fail after creating sandbox entry.</param>
    public void FailAfterPartial(bool failAfterPartial)
    {
        _options.FailAfterPartial = failAfterPartial;
    }

    /// <inheritdoc />
    public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Lookup and insert share one lock, so overlapping calls with the same key cannot both create a sandbox.
        lock (_provisionLock)
        {
            var existing = FindByIdempotencyKey(request.IdempotencyKey);
            if (existing is not null)
            {
                return Task.FromResult(new ProvisionResult
                {
                    Descriptor = existing.Descriptor,
                    NewlyProvisioned = false,
                });
            }

            if (request.Timeout <= TimeSpan.Zero)
            {
                throw new ProvisionTimeoutException("Timeout must be positive");
            }

            if (_provisionFailCount > 0)
            {
                _provisionFailCount--;
                throw new ProvisionTimeoutException("Injected provision failure");
            }

            var sandboxId = $"fake-{Guid.NewGuid():N}";
            var sandbox = new FakeSandbox
            {
                Id = sandboxId,
                IdempotencyKey = request.IdempotencyKey,
                SessionId = request.SessionId,
                Descriptor = CreateDescriptor(sandboxId, request),
                Spec = request.Spec,
                Status = SandboxStatus.Ready,
                Logs = new List<string> { $"Sandbox {sandboxId} provisioned" },
            };

            _sandboxes.TryAdd(sandboxId, sandbox);
            if (_options.FailAfterPartial)
            {
                throw new ProvisionTimeoutException("Injected failure after partial provision");
            }

            return Task.FromResult(new ProvisionResult
            {
                Descriptor = sandbox.Descriptor,
                NewlyProvisioned = true,
            });
        }
    }

    /// <inheritdoc />
    public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not found");
        }

        sandbox.Status = SandboxStatus.Attached;
        sandbox.Logs.Add($"Sandbox {sandboxId} attached");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync(sandboxId, request, cancellationToken);
    }

    private async IAsyncEnumerable<ExecEvent> ExecuteAsync(string sandboxId, ExecRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not found");
        }

        sandbox.Logs.Add($"Exec: {string.Join(" ", request.Argv)}");

        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        yield return new ExecEvent { Type = ExecEventType.Stdout, Stdout = $"Executed: {string.Join(" ", request.Argv)}\n" };
        yield return new ExecEvent { Type = ExecEventType.Exit, ExitCode = 0 };
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

        var start = cursor?.Position ?? 0;
        var follow = cursor?.Follow ?? false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var logs = sandbox.Logs;
            for (var i = (int)start; i < logs.Count; i++)
            {
                yield return logs[i];
            }

            if (!follow)
            {
                yield break;
            }

            start = logs.Count;
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out _))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not found");
        }

        return Task.FromResult(SnapshotResult.NotSupportedResult("Snapshots not supported by FakeSandboxProvider"));
    }

    /// <inheritdoc />
    public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
        {
            return Task.CompletedTask;
        }

        sandbox.Status = SandboxStatus.TearingDown;
        sandbox.Logs.Add($"Sandbox {sandboxId} tearing down");

        if (_teardownFailCount > 0)
        {
            _teardownFailCount--;
            sandbox.Status = SandboxStatus.Failed;
            throw new TeardownFailedException(sandboxId, _teardownFailCount + 1, "Injected teardown failure");
        }

        _sandboxes.TryRemove(sandboxId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a sandbox by ID (for testing).
    /// </summary>
    internal FakeSandbox? GetSandbox(string sandboxId)
    {
        _sandboxes.TryGetValue(sandboxId, out var sandbox);
        return sandbox;
    }

    /// <summary>
    /// Clears all sandboxes (for testing).
    /// </summary>
    public void Clear()
    {
        _sandboxes.Clear();
    }

    internal sealed class FakeSandbox
    {
        public required string Id { get; init; }

        public required string IdempotencyKey { get; init; }

        public required string SessionId { get; init; }

        public required SandboxDescriptor Descriptor { get; init; }

        public required SandboxSpec Spec { get; init; }

        public SandboxStatus Status { get; set; }

        public required List<string> Logs { get; init; }
    }

    private FakeSandbox? FindByIdempotencyKey(string idempotencyKey)
    {
        return _sandboxes.Values.FirstOrDefault(s => string.Equals(s.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));
    }

    private static SandboxDescriptor CreateDescriptor(string sandboxId, ProvisionRequest request)
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
                ["ompos.idempotency"] = request.IdempotencyKey,
            },
        };
    }
}
