namespace OmpOS.Harness.Registry;

/// <summary>
/// Why an export was refused.
/// </summary>
public enum RegistryRejectionKind
{
    /// <summary>
    /// The file is not a well-formed export: it is not JSON of the expected shape, its format version is unknown, or its
    /// entries are invalid or not in strictly ascending identifier order.
    /// </summary>
    Malformed,

    /// <summary>
    /// The file is well formed but its checksum does not match its entries.
    /// </summary>
    ChecksumMismatch,
}
