using Npgsql;

namespace Autonogrammer.Api.Audit;

/// <summary>
/// The <c>ledger_outbox</c> table, or a stand-in used when the process has no PostgreSQL data source. The gauge reads <see cref="Depth"/> and never calls this type.
/// </summary>
internal interface ILedgerOutboxStore
{
    /// <summary>Gets how many rows have not been delivered. The value is cached; reading it does no I/O.</summary>
    long Depth { get; }

    /// <summary>Inserts one row. A database failure is swallowed after a savepoint rollback so the caller's transaction stays usable.</summary>
    /// <param name="audit">The event to notarize.</param>
    /// <param name="transaction">The caller's transaction, when there is one.</param>
    /// <param name="cancellationToken">Cancels the insert.</param>
    /// <returns>A task that completes when the row is queued or the failure has been isolated.</returns>
    ValueTask EnqueueAsync(AuditLedgerEvent audit, NpgsqlTransaction? transaction, CancellationToken cancellationToken);

    /// <summary>Returns due rows, at most <paramref name="limit"/>.</summary>
    /// <param name="limit">How many rows the pump may take. It is the ledger in-flight cap.</param>
    /// <param name="now">The clock the due check uses.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The due rows.</returns>
    ValueTask<IReadOnlyList<LedgerOutboxItem>> ClaimDueAsync(int limit, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Marks a row delivered.</summary>
    /// <param name="id">The outbox row id.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>A task that completes when the row is marked.</returns>
    ValueTask MarkSentAsync(long id, CancellationToken cancellationToken);

    /// <summary>Records a failed send and the next time the pump may try.</summary>
    /// <param name="id">The outbox row id.</param>
    /// <param name="attempts">The attempt count after this failure.</param>
    /// <param name="nextAttemptAt">When the pump may try again.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>A task that completes when the schedule is stored.</returns>
    ValueTask ScheduleAsync(long id, int attempts, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);
}
