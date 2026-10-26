namespace OmpOS.Harness.Registry;

/// <summary>
/// The comparison of a UI snapshot with the omp-side view.
/// </summary>
/// <param name="Status">The outcome of the comparison.</param>
/// <param name="UiGeneration">The generation of the UI snapshot.</param>
/// <param name="UiChecksum">The lowercase hexadecimal SHA-256 of the UI snapshot's entries, as an export would carry it.</param>
/// <param name="OmpGeneration">The omp side's generation, or null when it has no accepted export.</param>
/// <param name="OmpChecksum">The omp side's checksum, or null when it has no accepted export.</param>
/// <param name="OmpRejection">Why the omp side's export was refused, when <paramref name="Status"/> is
/// <see cref="RegistryConsistencyStatus.OmpRejected"/>.</param>
public sealed record RegistryConsistencyReport(
    RegistryConsistencyStatus Status,
    long UiGeneration,
    string UiChecksum,
    long? OmpGeneration,
    string? OmpChecksum,
    RegistryRejectionKind? OmpRejection);
