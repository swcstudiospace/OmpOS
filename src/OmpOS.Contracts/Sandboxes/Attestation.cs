namespace OmpOS.Contracts.Sandboxes;

/// <summary>
/// Attestation evidence a sandbox can produce.
/// </summary>
public enum Attestation
{
    /// <summary>No attestation.</summary>
    None,

    /// <summary>Hardware quote (dstack-shaped contract).</summary>
    Quote,
}
