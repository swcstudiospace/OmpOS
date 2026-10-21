namespace OmpOS.Sandbox;

/// <summary>
/// Attestation boundary interface (dstack-shaped).
/// </summary>
public interface IAttestationBoundary
{
    /// <summary>
    /// Gets an attestation quote for the given nonce.
    /// </summary>
    /// <param name="nonce">The nonce to include in the quote.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attestation report.</returns>
    Task<AttestationReport> GetQuoteAsync(ReadOnlyMemory<byte> nonce, CancellationToken cancellationToken = default);
}
