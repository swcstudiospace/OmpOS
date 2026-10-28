namespace OmpOS.Contracts.Cloud;

/// <summary>The rules about statuses that both store implementations and the control plane apply, kept in one place so they cannot drift.</summary>
public static class CloudAgentStatusRules
{
    /// <summary>The statuses that count against the cap on concurrently active agents: the worker is, or is about to be, awake.</summary>
    public static IReadOnlyList<CloudAgentStatus> ActiveStatuses { get; } =
        [CloudAgentStatus.Provisioning, CloudAgentStatus.Starting, CloudAgentStatus.Running];

    /// <summary>Returns whether an agent in <paramref name="status"/> counts against the cap on concurrently active agents.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True for Provisioning, Starting and Running.</returns>
    public static bool IsActive(CloudAgentStatus status) =>
        status is CloudAgentStatus.Provisioning or CloudAgentStatus.Starting or CloudAgentStatus.Running;

    /// <summary>Returns whether an agent in <paramref name="status"/> may be moved to Starting by a wake request or a prompt.</summary>
    /// <param name="status">The status.</param>
    /// <returns>True for Idle, Stopped, Failed and Orphaned.</returns>
    public static bool CanActivate(CloudAgentStatus status) =>
        status is CloudAgentStatus.Idle or CloudAgentStatus.Stopped or CloudAgentStatus.Failed or CloudAgentStatus.Orphaned;

    /// <summary>Applies the activation move to a record: Starting, activity stamped, idle marker and error cleared.</summary>
    /// <param name="record">The record to move.</param>
    /// <param name="now">The instant to stamp it with.</param>
    /// <returns>The moved record.</returns>
    public static CloudAgentRecord Activate(CloudAgentRecord record, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record with { Status = CloudAgentStatus.Starting, UpdatedAt = now, LastActivityAt = now, IdleAt = null, Error = null };
    }
}
