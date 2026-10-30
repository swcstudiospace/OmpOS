namespace Autonogrammer.Sandbox;

/// <summary>
/// Default attestation boundary implementation (returns Unverified).
/// </summary>
public sealed class DefaultAttestationBoundary : IAttestationBoundary
{
    /// <inheritdoc />
    public Task<AttestationReport> GetQuoteAsync(ReadOnlyMemory<byte> nonce, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AttestationReport
        {
            Quote = nonce.ToArray(),
            Verdict = AttestationVerdict.Unverified,
            CertChain = null,
        });
    }
}
