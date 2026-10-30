namespace Autonogrammer.Coordination;

/// <summary>
/// Shared idempotency records keyed by scope and key. A record is pending while its request runs, and completed once the
/// response is stored. Pending records expire quickly so a crashed request can be retried. Completed records are kept so
/// that a repeated request can replay the stored response.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Claims a key in a scope for a request with the given fingerprint.</summary>
    /// <returns>
    /// <see cref="IdempotencyOutcome.Claimed"/> for the first caller. For later callers, the outcome depends on the stored record.
    /// </returns>
    Task<IdempotencyDecision> ClaimAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken);

    /// <summary>Stores the response for a claimed key, so later claims with the same fingerprint replay it.</summary>
    Task CompleteAsync(string scope, string key, string fingerprint, string responseJson, CancellationToken cancellationToken);

    /// <summary>Drops a pending claim so the same request can be retried. Completed records are never removed.</summary>
    Task ReleaseAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken);
}
