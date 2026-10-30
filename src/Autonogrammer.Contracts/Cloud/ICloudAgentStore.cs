namespace Autonogrammer.Contracts.Cloud;

/// <summary>
/// The durable store of cloud agent records. Every method that changes a record is atomic with the checks it states, so two API
/// instances sharing a store cannot both pass a cap. The store never sees a secret value: records hold secret names only.
/// </summary>
public interface ICloudAgentStore
{
    /// <summary>
    /// Stores a new record unless the owner's Idempotency-Key or caps say otherwise. The key lookup, the cap checks and the insert
    /// are one atomic step. A record whose <see cref="CloudAgentRecord.IdempotencyKey"/> is null skips the key check.
    /// </summary>
    /// <param name="record">The record to store, in <see cref="CloudAgentStatus.Provisioning"/>.</param>
    /// <param name="limits">The caps to hold the owner to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How the call ended and the record it concerns.</returns>
    Task<CloudAgentCreateResult> CreateAsync(CloudAgentRecord record, CloudAgentLimits limits, CancellationToken cancellationToken);

    /// <summary>Reads a record by agent identifier, whoever owns it. Callers scope by owner.</summary>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or null.</returns>
    Task<CloudAgentRecord?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Reads a record by the identifier of its session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or null.</returns>
    Task<CloudAgentRecord?> GetBySessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Looks up the record an owner created under an Idempotency-Key.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="idempotencyKey">The key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or null.</returns>
    Task<CloudAgentRecord?> FindByIdempotencyKeyAsync(string ownerId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Lists one owner's records, newest first.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyList<CloudAgentRecord>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    /// <summary>Lists the records of every owner whose status is one of <paramref name="statuses"/>, oldest update first.</summary>
    /// <param name="statuses">The statuses to match.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyList<CloudAgentRecord>> ListByStatusAsync(IReadOnlyCollection<CloudAgentStatus> statuses, CancellationToken cancellationToken);

    /// <summary>Lists every record, whoever owns it, oldest first. The orphan scan uses it.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyList<CloudAgentRecord>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies <paramref name="mutate"/> to the current record and stores the result, atomically: another change to the same record
    /// waits. <paramref name="mutate"/> is pure and may run more than once; it receives the current record, which it may return unchanged.
    /// </summary>
    /// <param name="id">The agent identifier.</param>
    /// <param name="mutate">Computes the new record from the current one. It must keep <see cref="CloudAgentRecord.Id"/>, owner and session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record after the change, or null when there is no such agent.</returns>
    Task<CloudAgentRecord?> UpdateAsync(string id, Func<CloudAgentRecord, CloudAgentRecord> mutate, CancellationToken cancellationToken);

    /// <summary>
    /// Moves an agent whose status is Idle, Stopped, Failed or Orphaned to Starting, unless its owner already has
    /// <paramref name="maxActive"/> other agents that are provisioning, starting or running. The count and the move are one atomic step.
    /// </summary>
    /// <param name="id">The agent identifier.</param>
    /// <param name="maxActive">The cap on active agents per owner.</param>
    /// <param name="now">The instant to stamp the record with.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How the call ended and the record after it.</returns>
    Task<CloudAgentActivation> TryActivateAsync(string id, int maxActive, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Removes a record.</summary>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a record was removed.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Counts what one owner uses of the caps.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The usage.</returns>
    Task<CloudAgentUsage> GetUsageAsync(string ownerId, CancellationToken cancellationToken);
}
