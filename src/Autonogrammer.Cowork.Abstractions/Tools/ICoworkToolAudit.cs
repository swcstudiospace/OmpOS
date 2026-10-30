namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Appends a local audit record. A write failure never fails the call, and nothing from it goes to the ledger.</summary>
public interface ICoworkToolAudit
{
    /// <summary>Records one call. The entry holds no arguments and no result bytes.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Cancels the write. A cancel or a write failure is swallowed by the implementation.</param>
    /// <returns>A task that completes when the record is stored or the failure has been swallowed.</returns>
    ValueTask RecordAsync(CoworkToolAuditEntry entry, CancellationToken cancellationToken);
}
