using System.Collections.Concurrent;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Sandbox;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>
/// The environment client of the test host: the shipped <see cref="FakeCloudEnvironmentClient"/> plus what a real provider does that the fake does not:
/// it asks the host's variable source for the worker's variables when a container is created (as the Railway client does), it can fail or stall creation,
/// it can lose a worker, and it implements the worker catalog (list with labels, remove) that the control plane needs.
/// </summary>
internal sealed class TestCloudEnvironment : ICloudEnvironmentClient, ICloudWorkerCatalog
{
    private readonly FakeCloudEnvironmentClient _inner = new();
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _variables = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _hidden = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<CloudWorkerInfo> _removed = new();
    private readonly List<CloudWorkerInfo> _extraWorkers = [];
    private int _failCreateTimes;
    private int _failRemoveTimes;

    /// <summary>Gets or sets the source the variables of a new container come from. Set by the host once its services exist.</summary>
    public Func<ICloudWorkerVariableSource>? VariableSource { get; set; }

    /// <summary>Gets or sets a gate that holds container creation until the test releases it, to observe an agent that is still provisioning.</summary>
    public TaskCompletionSource? CreateGate { get; set; }

    /// <summary>Gets or sets how many container creations fail with a transport error before one succeeds.</summary>
    public int FailCreateTimes
    {
        get => Volatile.Read(ref _failCreateTimes);
        set => Volatile.Write(ref _failCreateTimes, value);
    }

    /// <summary>Gets or sets how many worker removals fail with a transport error before one succeeds.</summary>
    public int FailRemoveTimes
    {
        get => Volatile.Read(ref _failRemoveTimes);
        set => Volatile.Write(ref _failRemoveTimes, value);
    }

    /// <summary>Gets the shipped fake, for its counters.</summary>
    public FakeCloudEnvironmentClient Fake => _inner;

    /// <summary>Gets the workers that were removed, in order.</summary>
    public IReadOnlyList<CloudWorkerInfo> Removed => [.. _removed];

    /// <summary>Gets the workers the catalog lists besides the ones created through the client: labelled orphans and workers that are not Autonogrammer's.</summary>
    public List<CloudWorkerInfo> ExtraWorkers => _extraWorkers;

    /// <inheritdoc />
    public bool SupportsInventory => true;

    /// <summary>Returns the variables a container was created with.</summary>
    /// <param name="sessionId">The session the container was created for.</param>
    /// <returns>The variables, or null when none were captured.</returns>
    public IReadOnlyDictionary<string, string>? VariablesOf(string sessionId) => _variables.GetValueOrDefault(sessionId);

    /// <summary>Makes the provider stop seeing a session's worker, as if it was deleted behind the control plane's back.</summary>
    /// <param name="sessionId">The session.</param>
    public void Hide(string sessionId) => _hidden[sessionId] = 0;

    /// <inheritdoc />
    public Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default) =>
        _hidden.ContainsKey(sessionId) ? Task.FromResult<CloudContainer?>(null) : _inner.FindContainerAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public async Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (CreateGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Interlocked.Decrement(ref _failCreateTimes) >= 0)
        {
            throw new CloudEnvironmentException("The provider is down.", CloudEnvironmentFailure.Transport);
        }

        _ = Interlocked.Exchange(ref _failCreateTimes, 0);
        if (VariableSource is { } source)
        {
            _variables[sessionId] = await source().ResolveAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        return await _inner.CreateContainerAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default) => _inner.FindVolumeAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default) => _inner.CreateVolumeAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default) =>
        _inner.AttachVolumeAsync(containerId, volumeId, mountPath, cancellationToken);

    /// <inheritdoc />
    public Task<CloudContainer> RequestIdleAsync(string containerId, CancellationToken cancellationToken = default) => _inner.RequestIdleAsync(containerId, cancellationToken);

    /// <inheritdoc />
    public Task<CloudContainer> EnsureAwakeAsync(string containerId, CancellationToken cancellationToken = default) => _inner.EnsureAwakeAsync(containerId, cancellationToken);

    /// <inheritdoc />
    public Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default) =>
        _inner.AppendVolumeFileAsync(volumeId, path, text, cancellationToken);

    /// <inheritdoc />
    public Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default) =>
        _inner.ReadVolumeFileAsync(volumeId, path, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudWorkerInfo>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CloudWorkerInfo>>([.. _extraWorkers]);

    /// <inheritdoc />
    public Task RemoveAsync(CloudWorkerInfo worker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        if (Interlocked.Decrement(ref _failRemoveTimes) >= 0)
        {
            throw new CloudEnvironmentException("The provider refused the removal.", CloudEnvironmentFailure.Transport);
        }

        _ = Interlocked.Exchange(ref _failRemoveTimes, 0);
        _removed.Enqueue(worker);
        _hidden[worker.SessionId] = 0;
        _ = _extraWorkers.RemoveAll(candidate => string.Equals(candidate.ContainerId, worker.ContainerId, StringComparison.Ordinal));
        return Task.CompletedTask;
    }
}
