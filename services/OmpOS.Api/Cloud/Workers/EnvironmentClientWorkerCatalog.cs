using OmpOS.Sandbox;

namespace OmpOS.Api.Cloud;

/// <summary>
/// The <see cref="ICloudWorkerCatalog"/> over the shipped environment clients. A Railway client removes a worker with <c>DeleteServiceAsync</c> and
/// <c>DeleteVolumeAsync</c>; a static pool releases the worker the session held (the always-warm worker itself stays). Neither client can list, so the
/// inventory is empty. Any other client is refused: a host that registers its own client registers its own catalog.
/// </summary>
internal sealed class EnvironmentClientWorkerCatalog : ICloudWorkerCatalog
{
    private readonly ICloudEnvironmentClient _client;

    /// <summary>Initializes a new instance of the <see cref="EnvironmentClientWorkerCatalog"/> class.</summary>
    /// <param name="client">The environment client.</param>
    public EnvironmentClientWorkerCatalog(ICloudEnvironmentClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public bool SupportsInventory => false;

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudWorkerInfo>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CloudWorkerInfo>>([]);

    /// <inheritdoc />
    public async Task RemoveAsync(CloudWorkerInfo worker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);
        switch (_client)
        {
            case RailwayCloudEnvironmentClient railway:
                try
                {
                    await railway.DeleteServiceAsync(worker.ContainerId, cancellationToken).ConfigureAwait(false);
                }
                catch (CloudEnvironmentException ex) when (ex.Failure == CloudEnvironmentFailure.NotFound)
                {
                    // The service is already gone: the removal is idempotent and goes on to the volume.
                }

                if (worker.VolumeId is { } volumeId)
                {
                    await railway.DeleteVolumeAsync(volumeId, cancellationToken).ConfigureAwait(false);
                }

                break;
            case StaticPoolEnvironmentClient pool:
                _ = await pool.ReleaseAsync(worker.SessionId, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new CloudEnvironmentException("This environment client cannot remove workers.", CloudEnvironmentFailure.Unsupported);
        }
    }
}
