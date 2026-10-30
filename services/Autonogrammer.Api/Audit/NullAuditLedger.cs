using Npgsql;

namespace Autonogrammer.Api.Audit;

/// <summary>
/// The ledger sink when <c>DataPlane:Ledger:Enabled</c> is false (the default). It writes nothing, so <c>ledger_outbox</c> stays empty, and it never opens a connection to Substrate.
/// </summary>
internal sealed class NullAuditLedger : IAuditLedger
{
    /// <inheritdoc />
    public ValueTask RecordAsync(AuditLedgerEvent audit, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> NotariseAsync(AuditLedgerEvent audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}
