using Npgsql;

namespace Autonogrammer.Api.Audit;

/// <summary>
/// The notarisation copy of an audit row. The default implementation does nothing, so <c>ledger_outbox</c> stays empty and no request waits on the ledger.
/// </summary>
internal interface IAuditLedger
{
    /// <summary>
    /// Queues a notarisation copy of an audit row the caller has written in <paramref name="transaction"/> (the same transaction as the state change).
    /// A failure is isolated: this does not throw and does not abort <paramref name="transaction"/>. It does not call the ledger.
    /// </summary>
    /// <param name="audit">The closed event. It carries no personal data.</param>
    /// <param name="transaction">The caller's transaction, or null when the caller has no database transaction.</param>
    /// <param name="cancellationToken">Cancels the enqueue. Cancellation is not a ledger failure.</param>
    /// <returns>A task that completes when the copy has been queued or deliberately dropped.</returns>
    ValueTask RecordAsync(AuditLedgerEvent audit, NpgsqlTransaction? transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Sends one queued event through <c>SubstrateGateway.EmitEventAsync</c>. The null ledger does not send.
    /// </summary>
    /// <param name="audit">The closed event.</param>
    /// <param name="cancellationToken">Cancels the send. The caller (the pump) passes its stopping token.</param>
    /// <returns><see langword="true"/> when the ledger stored the event.</returns>
    ValueTask<bool> NotariseAsync(AuditLedgerEvent audit, CancellationToken cancellationToken);
}
