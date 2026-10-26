namespace OmpOS.Coordination;

/// <summary>The outcome of an idempotency claim.</summary>
public enum IdempotencyOutcome
{
    /// <summary>The caller created the pending record and must do the work.</summary>
    Claimed,

    /// <summary>A completed record with the same fingerprint exists. The caller should replay its stored response.</summary>
    Replay,

    /// <summary>A pending record with the same fingerprint exists. Another request is doing the work.</summary>
    InProgress,

    /// <summary>A record with a different fingerprint exists for this key.</summary>
    Conflict,
}
