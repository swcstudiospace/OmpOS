using System.Globalization;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Cloud environment over a fixed list of already-running workers (tier T1 pooled workers, and the first live run on one
/// hand-made worker). Creating a container claims the first free worker for the session; nothing is created or stopped remotely.
/// </summary>
/// <remarks>
/// The container id is the worker id and <see cref="CloudContainer.BootGeneration"/> is always 1: a pooled worker is not restarted
/// by this client. <see cref="RequestIdleAsync"/> reports <see cref="CloudContainerPhase.Idle"/> and keeps the claim until
/// <see cref="ReleaseAsync"/> frees the worker. Volume files are only reachable inside the worker, so the volume file
/// methods throw <see cref="CloudEnvironmentException"/> with <see cref="CloudEnvironmentFailure.Unsupported"/>.
/// The claim table lives in memory and is guarded by a lock.
/// </remarks>
public sealed class StaticPoolEnvironmentClient : ICloudEnvironmentClient
{
    private const string VolumePrefix = "pool-";
    private const string UnsupportedFileMessage = "Volume files are only reachable inside the worker; the static pool has no file endpoint";

    private readonly Lock _gate = new();
    private readonly List<StaticPoolWorker> _workers;
    private readonly Dictionary<string, StaticPoolWorker> _workersById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _workerIdBySession = new(StringComparer.Ordinal);
    private readonly HashSet<string> _sessionsWithVolume = new(StringComparer.Ordinal);
    private readonly HashSet<string> _idleWorkerIds = new(StringComparer.Ordinal);
    private readonly WorkerReadinessProbe _probe;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticPoolEnvironmentClient"/> class.
    /// </summary>
    /// <param name="workerProbeClient">The HTTP client for health probes; owned by the caller.</param>
    /// <param name="options">The pool options; the worker list must be non-empty with unique ids and absolute http(s) URIs.</param>
    /// <param name="timeProvider">The clock for the wake window and the retry delay.</param>
    public StaticPoolEnvironmentClient(HttpClient workerProbeClient, StaticPoolEnvironmentOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(workerProbeClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options.Workers, nameof(options));
        if (options.Workers.Count == 0)
        {
            throw new ArgumentException("The pool needs at least one worker.", nameof(options));
        }

        foreach (var worker in options.Workers)
        {
            ArgumentNullException.ThrowIfNull(worker, nameof(options));
            ArgumentException.ThrowIfNullOrWhiteSpace(worker.WorkerId, nameof(options));
            ArgumentNullException.ThrowIfNull(worker.BaseUri, nameof(options));
            if (!worker.BaseUri.IsAbsoluteUri || (!string.Equals(worker.BaseUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(worker.BaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Worker '{worker.WorkerId}' needs an absolute http or https base URI."),
                    nameof(options));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(worker.VolumeMountPath, nameof(options));
            if (!_workersById.TryAdd(worker.WorkerId, worker))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"Worker id '{worker.WorkerId}' is not unique."),
                    nameof(options));
            }
        }

        _workers = [.. options.Workers];
        _probe = new WorkerReadinessProbe(workerProbeClient, timeProvider, options.WakeTimeout, options.WakeRetryInterval, options.HealthPath);
    }

    /// <inheritdoc />
    public Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            return Task.FromResult<CloudContainer?>(_workerIdBySession.TryGetValue(sessionId, out var workerId)
                ? ToContainer(_workersById[workerId], sessionId)
                : null);
        }
    }

    /// <inheritdoc />
    /// <exception cref="CloudEnvironmentException"><see cref="CloudEnvironmentFailure.CapacityExhausted"/> when every worker is claimed.</exception>
    public Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            if (_workerIdBySession.TryGetValue(sessionId, out var existing))
            {
                return Task.FromResult(ToContainer(_workersById[existing], sessionId));
            }

            var claimed = _workerIdBySession.Values.ToHashSet(StringComparer.Ordinal);
            var free = _workers.Find(worker => !claimed.Contains(worker.WorkerId))
                ?? throw new CloudEnvironmentException(
                    string.Create(CultureInfo.InvariantCulture, $"All {_workers.Count} pooled workers are claimed."),
                    CloudEnvironmentFailure.CapacityExhausted);
            _workerIdBySession[sessionId] = free.WorkerId;
            _idleWorkerIds.Remove(free.WorkerId);
            return Task.FromResult(ToContainer(free, sessionId));
        }
    }

    /// <inheritdoc />
    public Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            return Task.FromResult<CloudVolume?>(_sessionsWithVolume.Contains(sessionId) && _workerIdBySession.TryGetValue(sessionId, out var workerId)
                ? new CloudVolume(VolumePrefix + workerId, sessionId)
                : null);
        }
    }

    /// <inheritdoc />
    /// <exception cref="CloudEnvironmentException"><see cref="CloudEnvironmentFailure.NotFound"/> when the session holds no worker.</exception>
    public Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            if (!_workerIdBySession.TryGetValue(sessionId, out var workerId))
            {
                throw new CloudEnvironmentException(
                    string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' holds no pooled worker, so it has no volume."),
                    CloudEnvironmentFailure.NotFound);
            }

            _sessionsWithVolume.Add(sessionId);
            return Task.FromResult(new CloudVolume(VolumePrefix + workerId, sessionId));
        }
    }

    /// <inheritdoc />
    /// <exception cref="CloudEnvironmentException">
    /// <see cref="CloudEnvironmentFailure.NotFound"/> for an unknown worker or a volume of another worker, and
    /// <see cref="CloudEnvironmentFailure.Unsupported"/> when <paramref name="mountPath"/> is not the worker's mount path.
    /// </exception>
    public Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mountPath);
        var worker = RequireWorker(containerId);
        if (!string.Equals(volumeId, VolumePrefix + worker.WorkerId, StringComparison.Ordinal))
        {
            throw new CloudEnvironmentException(
                string.Create(CultureInfo.InvariantCulture, $"Volume '{volumeId}' does not belong to pooled worker '{worker.WorkerId}'."),
                CloudEnvironmentFailure.NotFound);
        }

        if (!string.Equals(mountPath, worker.VolumeMountPath, StringComparison.Ordinal))
        {
            throw new CloudEnvironmentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Pooled worker '{worker.WorkerId}' mounts its volume at '{worker.VolumeMountPath}'; it cannot be remounted at '{mountPath}'."),
                CloudEnvironmentFailure.Unsupported);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>A pooled worker is not stopped: the result is <see cref="CloudContainerPhase.Idle"/> and the claim is kept.</remarks>
    public Task<CloudContainer> RequestIdleAsync(string containerId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var worker = RequireWorker(containerId);
            var sessionId = SessionOf(worker.WorkerId);
            _idleWorkerIds.Add(worker.WorkerId);
            return Task.FromResult(ToContainer(worker, sessionId));
        }
    }

    /// <inheritdoc />
    /// <remarks>Probes the worker's health path with the shared wake loop; the boot generation stays 1.</remarks>
    public async Task<CloudContainer> EnsureAwakeAsync(string containerId, CancellationToken cancellationToken = default)
    {
        StaticPoolWorker worker;
        string sessionId;
        lock (_gate)
        {
            worker = RequireWorker(containerId);
            sessionId = SessionOf(worker.WorkerId);
        }

        await _probe.WaitUntilReadyAsync(worker.BaseUri, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _idleWorkerIds.Remove(worker.WorkerId);
            return ToContainer(worker, sessionId);
        }
    }

    /// <inheritdoc />
    /// <exception cref="CloudEnvironmentException">Always <see cref="CloudEnvironmentFailure.Unsupported"/>.</exception>
    public Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default) =>
        throw new CloudEnvironmentException(UnsupportedFileMessage, CloudEnvironmentFailure.Unsupported);

    /// <inheritdoc />
    /// <exception cref="CloudEnvironmentException">Always <see cref="CloudEnvironmentFailure.Unsupported"/>.</exception>
    public Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default) =>
        throw new CloudEnvironmentException(UnsupportedFileMessage, CloudEnvironmentFailure.Unsupported);

    /// <summary>
    /// Frees the worker a session holds so another session can claim it. The worker keeps running.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the session held a worker; <see langword="false"/> when it held none.</returns>
    public Task<bool> ReleaseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            _sessionsWithVolume.Remove(sessionId);
            if (!_workerIdBySession.Remove(sessionId, out var workerId))
            {
                return Task.FromResult(false);
            }

            _idleWorkerIds.Remove(workerId);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Sends one health request to a pooled worker.
    /// </summary>
    /// <param name="workerId">The pooled worker identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the worker answered 2xx; otherwise <see langword="false"/>.</returns>
    /// <exception cref="CloudEnvironmentException"><see cref="CloudEnvironmentFailure.NotFound"/> for an unknown worker id.</exception>
    public Task<bool> ProbeAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var worker = RequireWorker(workerId);
        return _probe.ProbeOnceAsync(worker.BaseUri, cancellationToken);
    }

    private StaticPoolWorker RequireWorker(string workerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        return _workersById.TryGetValue(workerId, out var worker)
            ? worker
            : throw new CloudEnvironmentException(
                string.Create(CultureInfo.InvariantCulture, $"Pooled worker '{workerId}' does not exist."),
                CloudEnvironmentFailure.NotFound);
    }

    // Callers hold _gate.
    private string SessionOf(string workerId)
    {
        foreach (var (sessionId, claimedWorkerId) in _workerIdBySession)
        {
            if (string.Equals(claimedWorkerId, workerId, StringComparison.Ordinal))
            {
                return sessionId;
            }
        }

        throw new CloudEnvironmentException(
            string.Create(CultureInfo.InvariantCulture, $"Pooled worker '{workerId}' is not claimed by any session."),
            CloudEnvironmentFailure.NotFound);
    }

    // Callers hold _gate.
    private CloudContainer ToContainer(StaticPoolWorker worker, string sessionId) =>
        new(worker.WorkerId, sessionId, _idleWorkerIds.Contains(worker.WorkerId) ? CloudContainerPhase.Idle : CloudContainerPhase.Running, 1)
        {
            WorkerBaseUri = worker.BaseUri,
        };
}
