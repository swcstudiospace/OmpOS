using System.Globalization;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// In-memory cloud environment: containers hold volatile process state that sleep destroys, volumes hold files that persist.
/// </summary>
internal sealed class FakeCloudEnvironmentClient : ICloudEnvironmentClient
{
    private readonly Dictionary<string, CloudContainer> _containersBySession = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sessionByContainer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CloudVolume> _volumesBySession = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _filesByVolume = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _processStateByContainer = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string VolumeId, string MountPath)> _mounts = new(StringComparer.Ordinal);

    /// <summary>Gets how many containers were created.</summary>
    public int ContainersCreated { get; private set; }

    /// <summary>Gets how many volumes were created.</summary>
    public int VolumesCreated { get; private set; }

    /// <summary>Gets how many times a container was put to sleep.</summary>
    public int SleepCalls { get; private set; }

    /// <summary>Gets how many times a container was woken.</summary>
    public int WakeCalls { get; private set; }

    /// <summary>Gets the volume mounted into a container, or null.</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <returns>The mounted volume identifier, or null.</returns>
    public string? MountedVolumeId(string containerId) =>
        _mounts.TryGetValue(containerId, out var mount) ? mount.VolumeId : null;

    /// <summary>Gets the path where a container mounts its volume, or null.</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <returns>The mount path, or null.</returns>
    public string? MountPathOf(string containerId) =>
        _mounts.TryGetValue(containerId, out var mount) ? mount.MountPath : null;

    /// <summary>Records a volatile in-container process fact (stands in for a running process or shell variable).</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="key">The fact name.</param>
    /// <param name="value">The fact value.</param>
    public void SetProcessState(string containerId, string key, string value)
    {
        if (!_processStateByContainer.TryGetValue(containerId, out var state))
        {
            state = new Dictionary<string, string>(StringComparer.Ordinal);
            _processStateByContainer[containerId] = state;
        }

        state[key] = value;
    }

    /// <summary>Gets how many volatile process facts a container still holds.</summary>
    /// <param name="containerId">The container identifier.</param>
    /// <returns>The number of facts.</returns>
    public int ProcessStateCount(string containerId) =>
        _processStateByContainer.TryGetValue(containerId, out var state) ? state.Count : 0;

    /// <inheritdoc />
    public Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_containersBySession.GetValueOrDefault(sessionId));

    /// <inheritdoc />
    public Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_containersBySession.ContainsKey(sessionId))
        {
            throw new InvalidOperationException($"Session '{sessionId}' already has a container.");
        }

        ContainersCreated++;
        var container = new CloudContainer(
            string.Create(CultureInfo.InvariantCulture, $"ctr-{ContainersCreated}"),
            sessionId,
            CloudContainerPhase.Running,
            1);
        _containersBySession[sessionId] = container;
        _sessionByContainer[container.ContainerId] = sessionId;
        return Task.FromResult(container);
    }

    /// <inheritdoc />
    public Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_volumesBySession.GetValueOrDefault(sessionId));

    /// <inheritdoc />
    public Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_volumesBySession.ContainsKey(sessionId))
        {
            throw new InvalidOperationException($"Session '{sessionId}' already has a volume.");
        }

        VolumesCreated++;
        var volume = new CloudVolume(string.Create(CultureInfo.InvariantCulture, $"vol-{VolumesCreated}"), sessionId);
        _volumesBySession[sessionId] = volume;
        _filesByVolume[volume.VolumeId] = new Dictionary<string, string>(StringComparer.Ordinal);
        return Task.FromResult(volume);
    }

    /// <inheritdoc />
    public Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default)
    {
        _mounts[containerId] = (volumeId, mountPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<CloudContainer> SleepContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        SleepCalls++;
        var sessionId = _sessionByContainer[containerId];
        var container = _containersBySession[sessionId] with { Phase = CloudContainerPhase.Sleeping };
        _containersBySession[sessionId] = container;
        _processStateByContainer.Remove(containerId);
        return Task.FromResult(container);
    }

    /// <inheritdoc />
    public Task<CloudContainer> WakeContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        WakeCalls++;
        var sessionId = _sessionByContainer[containerId];
        var current = _containersBySession[sessionId];
        var container = current with { Phase = CloudContainerPhase.Running, BootGeneration = current.BootGeneration + 1 };
        _containersBySession[sessionId] = container;
        return Task.FromResult(container);
    }

    /// <inheritdoc />
    public Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default)
    {
        var files = _filesByVolume[volumeId];
        files[path] = files.GetValueOrDefault(path, string.Empty) + text;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(_filesByVolume[volumeId].TryGetValue(path, out var text) ? text : null);
}
