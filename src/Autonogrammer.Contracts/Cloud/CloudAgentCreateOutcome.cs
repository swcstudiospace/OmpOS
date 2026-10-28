namespace Autonogrammer.Contracts.Cloud;

/// <summary>How <see cref="ICloudAgentStore.CreateAsync"/> ended.</summary>
public enum CloudAgentCreateOutcome
{
    /// <summary>The record was stored.</summary>
    Created,

    /// <summary>The owner already has a record under the same Idempotency-Key and the same request fingerprint; it is returned.</summary>
    Replayed,

    /// <summary>The owner already used the Idempotency-Key for a different request.</summary>
    IdempotencyKeyReused,

    /// <summary>The owner already has the maximum number of agents.</summary>
    AgentLimitReached,

    /// <summary>The owner already has the maximum number of active agents.</summary>
    ActiveLimitReached,
}
