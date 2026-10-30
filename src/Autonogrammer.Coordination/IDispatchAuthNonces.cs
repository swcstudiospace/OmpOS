namespace Autonogrammer.Coordination;

/// <summary>
/// Single-use device-hello challenge nonces. A nonce is claimed once and consumed once. An unreachable store fails closed:
/// both operations throw rather than treating the nonce as free.
/// </summary>
public interface IDispatchAuthNonces
{
    /// <summary>Stores the nonce hash if it is not already stored. The hash is the key; the nonce itself is never stored or logged.</summary>
    /// <param name="nonceSha256Hex">The SHA-256 of the nonce: 64 lowercase hexadecimal characters.</param>
    /// <param name="timeToLive">How long the claim lives. The hub uses 10 minutes. The lifetime is written in the same command as the claim.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns><see langword="true"/> when this call stored the nonce.</returns>
    Task<bool> TryClaimAsync(string nonceSha256Hex, TimeSpan timeToLive, CancellationToken cancellationToken);

    /// <summary>Deletes the nonce and reports whether it was present. A second consume, including a replayed hello, is <see langword="false"/>.</summary>
    /// <param name="nonceSha256Hex">The SHA-256 of the nonce: 64 lowercase hexadecimal characters.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns><see langword="true"/> when the nonce existed and was deleted.</returns>
    Task<bool> TryConsumeAsync(string nonceSha256Hex, CancellationToken cancellationToken);
}
