using Autonogrammer.Sandbox;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The environment client of a host that has cloud off (<c>Cloud:Enabled</c> false). The control-plane services take an <see cref="ICloudEnvironmentClient"/> by
/// constructor, so they can be built while cloud is off; no path reaches a worker then (the routes answer 404 first, the sweeper does nothing and a run of a cloud
/// session fails), and any call that still arrives here is a defect and fails loudly.
/// </summary>
internal sealed class DisabledCloudEnvironmentClient : ICloudEnvironmentClient
{
    /// <inheritdoc />
    public Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task<CloudContainer> RequestIdleAsync(string containerId, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task<CloudContainer> EnsureAwakeAsync(string containerId, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default) => throw Disabled();

    /// <inheritdoc />
    public Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default) => throw Disabled();

    private static InvalidOperationException Disabled() => new("Cloud agents are disabled: set Cloud:Enabled and Cloud:Environment:Provider.");
}
