namespace OmpOS.Api.Cloud;

/// <summary>
/// What the control plane needs to know about and do to workers beyond <c>ICloudEnvironmentClient</c>: list the workers a provider holds, with their
/// labels, and remove one. The environment client has neither operation (it finds a worker only by session), so the provider adapter implements this
/// port; where a provider cannot list, <see cref="SupportsInventory"/> is false and orphan workers can only be reported by name, not found.
/// </summary>
internal interface ICloudWorkerCatalog
{
    /// <summary>Gets whether <see cref="ListAsync"/> can list every worker the provider holds for OmpOS.</summary>
    bool SupportsInventory { get; }

    /// <summary>Lists the workers the provider holds, each with its labels.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The workers; empty when <see cref="SupportsInventory"/> is false.</returns>
    Task<IReadOnlyList<CloudWorkerInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Removes a worker and its volume. The caller has already checked the label; the provider adapter removes exactly what it is given.</summary>
    /// <param name="worker">The worker.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the worker is gone.</returns>
    Task RemoveAsync(CloudWorkerInfo worker, CancellationToken cancellationToken);
}
