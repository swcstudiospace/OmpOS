using Autonogrammer.Api.Audit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>An audit ledger that keeps every closed event it is given, so a test can read the fields that would leave the process.</summary>
internal sealed class RecordingAuditLedger : IAuditLedger
{
    private readonly List<AuditLedgerEvent> _events = [];

    /// <summary>Gets the events recorded so far.</summary>
    public IReadOnlyList<AuditLedgerEvent> Events
    {
        get
        {
            lock (_events)
            {
                return _events.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public ValueTask RecordAsync(AuditLedgerEvent audit, Npgsql.NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        lock (_events)
        {
            _events.Add(audit);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<bool> NotariseAsync(AuditLedgerEvent audit, CancellationToken cancellationToken) => ValueTask.FromResult(true);
}
