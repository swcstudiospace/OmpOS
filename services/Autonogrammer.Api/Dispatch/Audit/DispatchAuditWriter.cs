using Autonogrammer.Api.Audit;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Dispatch.Audit;

/// <summary>
/// Writes the closed audit events of the device channel to the audit ledger. An event carries the command identifier, a fixed outcome, a fixed
/// summary and the account only as its keyed tenant hash. With the ledger off (the default) the sink writes nothing.
/// </summary>
internal sealed class DispatchAuditWriter(IAuditLedger ledger, ITenantHasher tenants)
{
    /// <summary>Records one device input the hub did not apply. A command identifier that is not a ULID is not recorded, because the ledger item must be one.</summary>
    /// <param name="accountId">The owning account.</param>
    /// <param name="commandId">The command the frame named.</param>
    /// <param name="summary">The fixed summary of the rejection.</param>
    /// <param name="cancellationToken">Cancels the enqueue.</param>
    /// <returns>A task that completes when the event is queued or dropped.</returns>
    public ValueTask RecordRejectedAsync(string accountId, string commandId, string summary, CancellationToken cancellationToken)
    {
        if (!DispatchCommandIds.IsValid(commandId))
        {
            return ValueTask.CompletedTask;
        }

        var audit = new AuditLedgerEvent
        {
            Item = commandId,
            Outcome = "rejected",
            Summary = summary,
            TenantHash = tenants.HashTenant(accountId),
        };
        return ledger.RecordAsync(audit, null, cancellationToken);
    }
}
