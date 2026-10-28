namespace OmpOS.Api.Cloud;

/// <summary>The label check that stands before every worker removal. A worker is removed only when the label <see cref="CloudWorkerLabels.AgentKey"/> proves it belongs to the agent being deleted, or, for an orphan, names an agent that has no record.</summary>
internal static class CloudWorkerGuard
{
    /// <summary>Returns whether a worker may be removed on behalf of one agent.</summary>
    /// <param name="worker">The worker.</param>
    /// <param name="agentId">The agent being deleted.</param>
    /// <returns>True when the worker's label names exactly this agent.</returns>
    public static bool BelongsTo(CloudWorkerInfo worker, string agentId)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return worker.Labels.TryGetValue(CloudWorkerLabels.AgentKey, out var label) && string.Equals(label, agentId, StringComparison.Ordinal);
    }

    /// <summary>Returns whether a worker is an orphan that may be cleaned up: it carries the label, and the label names no known agent.</summary>
    /// <param name="worker">The worker.</param>
    /// <param name="knownAgentIds">The identifiers of every agent that has a record.</param>
    /// <returns>True when the worker is labelled and no record claims it.</returns>
    public static bool IsRemovableOrphan(CloudWorkerInfo worker, IReadOnlySet<string> knownAgentIds)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(knownAgentIds);
        return worker.Labels.TryGetValue(CloudWorkerLabels.AgentKey, out var label)
            && !string.IsNullOrWhiteSpace(label)
            && !knownAgentIds.Contains(label);
    }
}
