namespace Autonogrammer.Harness.Registry;

/// <summary>
/// Compares the registry snapshot the UI shows with the registry the omp side reads, by generation and checksum.
/// </summary>
public static class RegistryConsistencyChecker
{
    /// <summary>
    /// Compares a UI snapshot with the omp-side view.
    /// </summary>
    /// <param name="uiSnapshot">The snapshot the UI list shows.</param>
    /// <param name="ompView">The omp-side view.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The report. I/O failures are thrown; content failures are reported in the result.</returns>
    public static async Task<RegistryConsistencyReport> CheckAsync(
        RegistrySnapshot uiSnapshot,
        IOmpRegistryView ompView,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uiSnapshot);
        ArgumentNullException.ThrowIfNull(ompView);

        var uiChecksum = RegistryExport.ComputeChecksum([.. uiSnapshot.Entries]);
        var omp = await ompView.ReadAsync(uiSnapshot.Generation, cancellationToken).ConfigureAwait(false);

        if (omp.Snapshot is not { } ompSnapshot)
        {
            var status = omp.Status == RegistryReadStatus.Rejected
                ? RegistryConsistencyStatus.OmpRejected
                : RegistryConsistencyStatus.OmpMissing;
            return new RegistryConsistencyReport(status, uiSnapshot.Generation, uiChecksum, null, null, omp.Rejection);
        }

        var ompChecksum = RegistryExport.ComputeChecksum([.. ompSnapshot.Entries]);
        var consistency = ompSnapshot.Generation != uiSnapshot.Generation
            ? RegistryConsistencyStatus.GenerationDrift
            : string.Equals(ompChecksum, uiChecksum, StringComparison.Ordinal)
                ? RegistryConsistencyStatus.Consistent
                : RegistryConsistencyStatus.ChecksumMismatch;

        return new RegistryConsistencyReport(consistency, uiSnapshot.Generation, uiChecksum, ompSnapshot.Generation, ompChecksum, null);
    }
}
