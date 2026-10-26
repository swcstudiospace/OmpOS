namespace OmpOS.Harness.Registry;

/// <summary>
/// How the UI snapshot and the omp-side view of a registry compare.
/// </summary>
public enum RegistryConsistencyStatus
{
    /// <summary>
    /// Both sides report the same generation and the same checksum.
    /// </summary>
    Consistent,

    /// <summary>
    /// The omp side reports a different generation from the UI snapshot.
    /// </summary>
    GenerationDrift,

    /// <summary>
    /// Both sides report the same generation but different content, which a single writer never produces.
    /// </summary>
    ChecksumMismatch,

    /// <summary>
    /// The omp side has no export.
    /// </summary>
    OmpMissing,

    /// <summary>
    /// The omp side has an export that was refused as malformed or checksum-mismatched.
    /// </summary>
    OmpRejected,
}
