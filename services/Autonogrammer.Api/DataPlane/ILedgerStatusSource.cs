using Autonogrammer.Resilience;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// What the audit-ledger sink tells the status service. The sink (plan 68-05) registers one implementation; without it the status of an enabled ledger
/// is reported as not configured.
/// </summary>
internal interface ILedgerStatusSource
{
    /// <summary>Gets what the breaker of the ledger pipeline says.</summary>
    DependencyState State { get; }

    /// <summary>Gets the number of outbox rows that have not been delivered.</summary>
    long OutboxDepth { get; }
}
