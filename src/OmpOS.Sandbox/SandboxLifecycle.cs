using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;

namespace OmpOS.Sandbox;

/// <summary>
/// Manages sandbox lifecycle: TTL, orphan reaping, retried teardown with jittered backoff.
/// </summary>
public sealed class SandboxLifecycle : ISandboxReaper
{
    private static readonly Action<ILogger, int, int, string, Exception?> TeardownAttemptFailed =
        LoggerMessage.Define<int, int, string>(
            LogLevel.Warning,
            new EventId(1, "TeardownAttemptFailed"),
            "Teardown attempt {Attempt}/{MaxAttempts} failed for sandbox {SandboxId}");

    private static readonly Action<ILogger, string, Exception?> ReapFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2, "ReapFailed"),
            "Failed to reap sandbox {SandboxId}");

    private readonly ISandboxProvider _provider;
    private readonly TimeProvider _timeProvider;
    private readonly SandboxLifecycleOptions _options;
    private readonly ILogger<SandboxLifecycle> _logger;
    private readonly Random _random;
    private readonly ConcurrentDictionary<string, SandboxRegistryEntry> _registry = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxLifecycle"/> class.
    /// </summary>
    /// <param name="provider">The sandbox provider.</param>
    /// <param name="timeProvider">The time provider (for testing with fake clock).</param>
    /// <param name="options">The lifecycle options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="random">Random for jittered backoff (injectable for testing).</param>
    public SandboxLifecycle(
        ISandboxProvider provider,
        TimeProvider timeProvider,
        IOptions<SandboxLifecycleOptions> options,
        ILogger<SandboxLifecycle> logger,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _provider = provider;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
        _random = random ?? Random.Shared;
    }

    /// <summary>
    /// Provisions a sandbox through the lifecycle manager.
    /// Registers the sandbox and ensures failed provisions never leave session Running.
    /// </summary>
    /// <param name="request">The provision request.</param>
    /// <param name="sessionStatusProvider">Callback to get session status (for orphan detection).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provision result.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Unexpected provider failures are wrapped as ProvisionTimeoutException so callers see a single typed failure; typed SandboxExceptions pass through untouched.")]
    public async Task<ProvisionResult> ProvisionAsync(
        ProvisionRequest request,
        Func<string, Task<string?>> sessionStatusProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sessionStatusProvider);

        var now = _timeProvider.GetUtcNow();

        string? sessionStatus;
        try
        {
            sessionStatus = await sessionStatusProvider(request.SessionId).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ProvisionTimeoutException($"Provisioning failed: {ex.Message}", ex);
        }

        if (!string.Equals(sessionStatus, "Running", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Session {request.SessionId} is not Running (status: {sessionStatus ?? "unknown"})");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(request.Timeout);

            var result = await _provider.ProvisionAsync(request, cts.Token).ConfigureAwait(false);

            var entry = new SandboxRegistryEntry
            {
                SandboxId = result.Descriptor.Id,
                SessionId = request.SessionId,
                IdempotencyKey = request.IdempotencyKey,
                ProvisionedAt = now,
                Ttl = _options.Ttl,
                SessionStatusProvider = sessionStatusProvider,
                Status = SandboxStatus.Ready,
                Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ompos.session"] = request.SessionId,
                    ["ompos.idempotency"] = request.IdempotencyKey,
                },
            };
            _registry.TryAdd(result.Descriptor.Id, entry);

            return result;
        }
        catch (Exception ex) when (ex is not SandboxException)
        {
            throw new ProvisionTimeoutException($"Provisioning failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attaches to a sandbox.
    /// </summary>
    public async Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_registry.TryGetValue(sandboxId, out var entry))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not registered");
        }

        await _provider.AttachAsync(sandboxId, cancellationToken).ConfigureAwait(false);
        entry.Status = SandboxStatus.Attached;
    }

    /// <summary>
    /// Executes a command in a sandbox.
    /// </summary>
    public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
    {
        if (!_registry.TryGetValue(sandboxId, out _))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not registered");
        }

        return _provider.ExecAsync(sandboxId, request, cancellationToken);
    }

    /// <summary>
    /// Streams logs from a sandbox.
    /// </summary>
    public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
    {
        if (!_registry.TryGetValue(sandboxId, out _))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not registered");
        }

        return _provider.StreamLogsAsync(sandboxId, cursor, cancellationToken);
    }

    /// <summary>
    /// Takes a snapshot of a sandbox.
    /// </summary>
    public async Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_registry.TryGetValue(sandboxId, out _))
        {
            throw new NotAvailableException($"Sandbox {sandboxId} not registered");
        }

        return await _provider.SnapshotAsync(sandboxId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tears down a sandbox with jittered exponential backoff retry.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Teardown must be retried against any transient provider failure; terminal failures surface as typed TeardownFailedException after retries are exhausted.")]
    public async Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
    {
        if (!_registry.TryGetValue(sandboxId, out var entry))
        {
            return;
        }

        entry.Status = SandboxStatus.TearingDown;

        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _options.TeardownRetryCount)
        {
            attempt++;
            try
            {
                await _provider.TeardownAsync(sandboxId, cancellationToken).ConfigureAwait(false);
                entry.Status = SandboxStatus.Destroyed;
                _registry.TryRemove(sandboxId, out _);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                TeardownAttemptFailed(_logger, attempt, _options.TeardownRetryCount, sandboxId, ex);

                if (attempt < _options.TeardownRetryCount)
                {
                    var baseDelay = _options.BaseBackoff.TotalMilliseconds * Math.Pow(2, attempt - 1);
                    var delay = ComputeJitteredDelay(baseDelay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        entry.Status = SandboxStatus.Failed;
        throw new TeardownFailedException(sandboxId, attempt, $"Teardown failed after {attempt} attempts", lastException!);
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The reaper is best-effort: one sandbox's teardown failure must not stop the sweep; failures are logged and counted as not reaped.")]
    public async Task<int> ReapOnceAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var reaped = 0;

        foreach (var entry in _registry.Values.ToList())
        {
            try
            {
                var isExpired = now - entry.ProvisionedAt >= entry.Ttl;
                var isOrphaned = await IsOrphanedAsync(entry).ConfigureAwait(false);
                if (isExpired || isOrphaned)
                {
                    await TeardownAsync(entry.SandboxId, cancellationToken).ConfigureAwait(false);
                    reaped++;
                }
            }
            catch (Exception ex)
            {
                ReapFailed(_logger, entry.SandboxId, ex);
            }
        }

        return reaped;
    }

    private static async Task<bool> IsOrphanedAsync(SandboxRegistryEntry entry)
    {
        var status = await entry.SessionStatusProvider(entry.SessionId).ConfigureAwait(false);
        return !string.Equals(status, "Running", StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets a registry entry by sandbox ID.
    /// </summary>
    public SandboxRegistryEntry? GetEntry(string sandboxId)
    {
        _registry.TryGetValue(sandboxId, out var entry);
        return entry;
    }

    /// <summary>
    /// Gets all registry entries.
    /// </summary>
    public IReadOnlyList<SandboxRegistryEntry> GetAllEntries()
    {
        return _registry.Values.ToList();
    }

    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "Randomness here is backoff jitter, not a security decision; the Random instance is injected for deterministic tests.")]
    private TimeSpan ComputeJitteredDelay(double baseDelayMilliseconds)
    {
        return ComputeEqualJitterDelay(baseDelayMilliseconds, _random);
    }

    /// <summary>
    /// Computes an equal-jitter delay: half the base plus a uniform draw over the other
    /// half, capped at 30 seconds total. Retries can never collapse to ~0ms.
    /// </summary>
    /// <param name="baseDelayMilliseconds">The exponential backoff base in milliseconds.</param>
    /// <param name="random">The randomness source.</param>
    /// <returns>The jittered delay.</returns>
    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "Randomness here is backoff jitter, not a security decision; the Random instance is injected for deterministic tests.")]
    internal static TimeSpan ComputeEqualJitterDelay(double baseDelayMilliseconds, Random random)
    {
        var delay = (baseDelayMilliseconds / 2) + (random.NextDouble() * baseDelayMilliseconds / 2);
        return TimeSpan.FromMilliseconds(Math.Min(delay, 30000));
    }
}
