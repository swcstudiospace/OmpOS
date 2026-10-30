namespace Autonogrammer.Contracts.Cloud;

/// <summary>How <see cref="ICloudAgentStore.TryActivateAsync"/> ended.</summary>
public enum CloudAgentActivationOutcome
{
    /// <summary>The agent moved to <see cref="CloudAgentStatus.Starting"/>.</summary>
    Activated,

    /// <summary>The agent was already provisioning, starting or running; nothing changed.</summary>
    AlreadyActive,

    /// <summary>The owner already has the maximum number of active agents; nothing changed.</summary>
    LimitReached,

    /// <summary>No such agent.</summary>
    NotFound,

    /// <summary>The agent's status cannot be woken (it is stopping, being deleted or over its compute limit).</summary>
    NotWakeable,
}
