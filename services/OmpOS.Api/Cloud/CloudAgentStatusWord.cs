using OmpOS.Contracts.Cloud;

namespace OmpOS.Api.Cloud;

/// <summary>Words a <see cref="CloudAgentStatus"/> the way the wire writes it, for problem details and codes.</summary>
internal static class CloudAgentStatusWord
{
    /// <summary>Gets the wire word of a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The lower-case word, for example <c>limit_reached</c>.</returns>
    public static string Of(CloudAgentStatus status) => status switch
    {
        CloudAgentStatus.Provisioning => "provisioning",
        CloudAgentStatus.Starting => "starting",
        CloudAgentStatus.Running => "running",
        CloudAgentStatus.Idle => "idle",
        CloudAgentStatus.Stopping => "stopping",
        CloudAgentStatus.Stopped => "stopped",
        CloudAgentStatus.Failed => "failed",
        CloudAgentStatus.Deleting => "deleting",
        CloudAgentStatus.Orphaned => "orphaned",
        CloudAgentStatus.LimitReached => "limit_reached",
        _ => "unknown",
    };
}
