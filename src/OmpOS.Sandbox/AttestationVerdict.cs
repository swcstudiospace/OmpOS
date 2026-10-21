namespace OmpOS.Sandbox;

/// <summary>
/// Attestation verification verdict.
/// </summary>
public enum AttestationVerdict
{
    /// <summary>Not verified (default for stub).</summary>
    Unverified,

    /// <summary>Verified successfully.</summary>
    Verified,

    /// <summary>Verification failed.</summary>
    Failed,
}
