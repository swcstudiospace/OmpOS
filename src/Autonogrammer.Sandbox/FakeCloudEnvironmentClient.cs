using System.Globalization;

namespace Autonogrammer.Sandbox;

/// <summary>
/// In-memory cloud environment: containers hold volatile process state that sleep destroys, volumes hold files that persist.
/// Safe for concurrent callers: every operation runs under one lock.
/// </summary>
public sealed class FakeCloudEnvironmentClient : ICloudEnvironmentClient
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, CloudContainer> _containersBySession = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sessionByContainer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CloudVolume> _volumesBySession = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _filesByVolume = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _processStateByContainer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string VolumeId, string MountPath)> _mounts = new(StringComparer.Ordinal);
    private int _containersCreated;
    private int _volumesCreated;
    private int _idleCalls;
    private int _ensureAwakeCalls;
    private int _failEnsureAwakeTimes;

    /// <summary>Gets how many containers were created.</summary>
    public int ContainersCreated
    {
        get
        {
            lock (_gate)
            {
                return _containersCreated;
            }
        }
    }

    /// <summary>Gets how many volumes were created.</summary>
    public int VolumesCreated
    {
        get
        {
            lock (_gate)
            {
                return _volumesCreated;
            }
        }
    }

    /// <summary>Gets how many times a container was declared idle (and slept at once).</summary>
    public int IdleCalls
    {
        get
        {
            lock (_gate)
            {
                return _idleCalls;
            }
        }
    }

    /// <summary>Gets how many times a container was asked to be awake, including calls that failed.</summary>
    public int EnsureAwakeCalls
    {
        get
        {
            lock (_gate)
            {
                return _ensureAwakeCalls;
            }
        }
    }

    /// <summary>
    /// Gets or sets how many further <see cref="EnsureAwakeAsync"/> calls throw
    /// <see cref="CloudEnvironmentException"/> with <see cref="CloudEnvironmentFailure.WakeTimeout"/> before one succeeds.
    /// </summary>
    public int FailEnsureAwakeTimes
    {
        get
        {
            lock (_gate)
            {
                return _failEnsureAwakeTimes;
            }
        }

        set
        {
            lock (_gate)
            {
                _failEnsureAwakeTimes = value;
            }
        }
    }

    /// <summary>Gets the volume mounted into a container, or null.</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <returns>The mounted volume identifier, or null.</returns>
    public string? MountedVolumeId(string containerId)
    {
        lock (_gate)
        {
            return _mounts.TryGetValue(containerId, out var mount) ? mount.VolumeId : null;
        }
    }

    /// <summary>Gets the path where a container mounts its volume, or null.</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <returns>The mount path, or null.</returns>
    public string? MountPathOf(string containerId)
    {
        lock (_gate)
        {
            return _mounts.TryGetValue(containerId, out var mount) ? mount.MountPath : null;
        }
    }

    /// <summary>Records a volatile in-container process fact (stands in for a running process or shell variable).</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="key">The fact name.</param>
    /// <param name="value">The fact value.</param>
    public void SetProcessState(string containerId, string key, string value)
    {
        lock (_gate)
        {
            if (!_processStateByContainer.TryGetValue(containerId, out var state))
            {
                state = new Dictionary<string, string>(StringComparer.Ordinal);
                _processStateByContainer[containerId] = state;
            }

            state[key] = value;
        }
    }

    /// <summary>Gets how many volatile process facts a container still holds.</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <returns>The number of facts.</returns>
    public int ProcessStateCount(string containerId)
    {
        lock (_gate)
        {
            return _processStateByContainer.TryGetValue(containerId, out var state) ? state.Count : 0;
        }
    }

    /// <inheritdoc />
    public Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_containersBySession.GetValueOrDefault(sessionId));
        }
    }

    /// <inheritdoc />
    /// <remarks>The container gets the deterministic worker base URI <c>http://fake-worker-{n}.invalid/</c>, n counting from 1.</remarks>
    public Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_containersBySession.ContainsKey(sessionId))
            {
                throw new InvalidOperationException($"Session '{sessionId}' already has a container.");
            }

            _containersCreated++;
            var container = new CloudContainer(
                string.Create(CultureInfo.InvariantCulture, $"ctr-{_containersCreated}"),
                sessionId,
                CloudContainerPhase.Running,
                1)
            {
                WorkerBaseUri = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://fake-worker-{_containersCreated}.invalid/")),
            };
            _containersBySession[sessionId] = container;
            _sessionByContainer[container.ContainerId] = sessionId;
            return Task.FromResult(container);
        }
    }

    /// <inheritdoc />
    public Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_volumesBySession.GetValueOrDefault(sessionId));
        }
    }

    /// <inheritdoc />
    public Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_volumesBySession.ContainsKey(sessionId))
            {
                throw new InvalidOperationException($"Session '{sessionId}' already has a volume.");
            }

            _volumesCreated++;
            var volume = new CloudVolume(string.Create(CultureInfo.InvariantCulture, $"vol-{_volumesCreated}"), sessionId);
            _volumesBySession[sessionId] = volume;
            _filesByVolume[volume.VolumeId] = new Dictionary<string, string>(StringComparer.Ordinal);
            return Task.FromResult(volume);
        }
    }

    /// <inheritdoc />
    public Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _mounts[containerId] = (volumeId, mountPath);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    /// <remarks>The fake sleeps at once: the phase becomes Sleeping and the container's volatile process state is cleared.</remarks>
    public Task<CloudContainer> RequestIdleAsync(string containerId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _idleCalls++;
            var sessionId = _sessionByContainer[containerId];
            var container = _containersBySession[sessionId] with { Phase = CloudContainerPhase.Sleeping };
            _containersBySession[sessionId] = container;
            _processStateByContainer.Remove(containerId);
            return Task.FromResult(container);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A running container is returned as is. A container that was not running becomes running with an incremented boot generation.
    /// While <see cref="FailEnsureAwakeTimes"/> is positive the call throws a wake timeout and changes nothing.
    /// </remarks>
    public Task<CloudContainer> EnsureAwakeAsync(string containerId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _ensureAwakeCalls++;
            if (_failEnsureAwakeTimes > 0)
            {
                _failEnsureAwakeTimes--;
                throw new CloudEnvironmentException(
                    string.Create(CultureInfo.InvariantCulture, $"Container '{containerId}' did not wake in time."),
                    CloudEnvironmentFailure.WakeTimeout);
            }

            var sessionId = _sessionByContainer[containerId];
            var current = _containersBySession[sessionId];
            if (current.Phase == CloudContainerPhase.Running)
            {
                return Task.FromResult(current);
            }

            var container = current with { Phase = CloudContainerPhase.Running, BootGeneration = current.BootGeneration + 1 };
            _containersBySession[sessionId] = container;
            return Task.FromResult(container);
        }
    }

    /// <inheritdoc />
    public Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var files = _filesByVolume[volumeId];
            files[path] = files.GetValueOrDefault(path, string.Empty) + text;
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_filesByVolume[volumeId].TryGetValue(path, out var text) ? text : null);
        }
    }
}
