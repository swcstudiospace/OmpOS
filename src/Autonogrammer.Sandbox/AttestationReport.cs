namespace Autonogrammer.Sandbox;

/// <summary>
/// Attestation report from an enclave.
/// </summary>
public sealed record AttestationReport
{
    /// <summary>Gets the raw quote bytes.</summary>
    public required IReadOnlyList<byte> Quote { get; init; }

    /// <summary>Gets the verification verdict.</summary>
    public required AttestationVerdict Verdict { get; init; }

    /// <summary>Gets the cert chain (if available).</summary>
    public IReadOnlyList<byte[]>? CertChain { get; init; }
}
