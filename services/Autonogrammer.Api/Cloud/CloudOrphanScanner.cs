using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Sandbox;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Compares the agent records with the workers the provider holds. A record that names a worker the provider no longer has is marked orphaned. A
/// labelled worker that no record claims is reported and, only when cleanup is on, removed after the record is checked once more. A worker without the
/// label <see cref="CloudWorkerLabels.AgentKey"/> is never touched: it is not Autonogrammer's to delete.
/// </summary>
internal sealed class CloudOrphanScanner
{
    private readonly ICloudAgentStore _store;
    private readonly ICloudEnvironmentClient _client;
    private readonly ICloudWorkerCatalog _catalog;
    private readonly CloudAgentLifecycle _lifecycle;
    private readonly ILogger<CloudOrphanScanner> _logger;

    /// <summary>Initializes a new instance of the <see cref="CloudOrphanScanner"/> class.</summary>
    /// <param name="store">The agent store.</param>
    /// <param name="client">The environment client, to look workers up by session.</param>
    /// <param name="catalog">The worker catalog, to list and remove workers.</param>
    /// <param name="lifecycle">The worker lifecycle, to mark agents orphaned.</param>
    /// <param name="logger">The logger.</param>
    public CloudOrphanScanner(ICloudAgentStore store, ICloudEnvironmentClient client, ICloudWorkerCatalog catalog, CloudAgentLifecycle lifecycle, ILogger<CloudOrphanScanner> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _client = client;
        _catalog = catalog;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    /// <summary>Scans once.</summary>
    /// <param name="cleanup">Whether to remove labelled workers that no record claims.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The report.</returns>
    public async Task<CloudOrphanReport> ScanAsync(bool cleanup, CancellationToken cancellationToken)
    {
        var records = await _store.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var orphanedRecords = new List<string>();
        foreach (var record in records)
        {
            if (record.WorkerRef is null
                || record.Status is not (CloudAgentStatus.Running or CloudAgentStatus.Idle or CloudAgentStatus.Stopped or CloudAgentStatus.Starting))
            {
                continue;
            }

            CloudContainer? found;
            try
            {
                found = await _client.FindContainerAsync(record.SessionId, cancellationToken).ConfigureAwait(false);
            }
            catch (CloudEnvironmentException ex)
            {
                Log.LookupFailed(_logger, record.Id, ex.Failure.ToString());
                continue;
            }

            if (found is null || !string.Equals(found.ContainerId, record.WorkerRef, StringComparison.Ordinal))
            {
                await _lifecycle.MarkOrphanedAsync(record.Id, new CloudAgentError { Code = "worker_missing", Message = "The worker for this agent no longer exists at the provider." }, cancellationToken).ConfigureAwait(false);
                orphanedRecords.Add(record.Id);
            }
        }

        var withoutRecord = new List<string>();
        var removed = new List<string>();
        var unlabelled = 0;
        if (_catalog.SupportsInventory)
        {
            var known = records.Select(static record => record.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var worker in await _catalog.ListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!worker.Labels.ContainsKey(CloudWorkerLabels.AgentKey))
                {
                    unlabelled++;
                    continue;
                }

                if (!CloudWorkerGuard.IsRemovableOrphan(worker, known))
                {
                    continue;
                }

                withoutRecord.Add(worker.ContainerId);
                if (cleanup && await StillOrphanAsync(worker, cancellationToken).ConfigureAwait(false))
                {
                    await _catalog.RemoveAsync(worker, cancellationToken).ConfigureAwait(false);
                    removed.Add(worker.ContainerId);
                }
            }
        }

        return new CloudOrphanReport(orphanedRecords, withoutRecord, removed, unlabelled, _catalog.SupportsInventory);
    }

    private async Task<bool> StillOrphanAsync(CloudWorkerInfo worker, CancellationToken cancellationToken)
    {
        // The record may have been created after the listing: look again, by the label and by the session, just before removing.
        var label = worker.Labels[CloudWorkerLabels.AgentKey];
        return await _store.GetAsync(label, cancellationToken).ConfigureAwait(false) is null
            && await _store.GetBySessionAsync(worker.SessionId, cancellationToken).ConfigureAwait(false) is null;
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, string, Exception?> LookupFailedMessage =
            LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(73, "CloudOrphanLookupFailed"), "The orphan scan could not look up the worker of cloud agent {AgentId}: {Failure}.");

        public static void LookupFailed(ILogger logger, string agentId, string failure) => LookupFailedMessage(logger, agentId, failure, null);
    }
}
