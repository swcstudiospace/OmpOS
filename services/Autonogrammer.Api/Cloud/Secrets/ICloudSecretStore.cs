using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The write-only secret store: a caller can put a value, learn that it exists and when it was written, and delete it, but no method here
/// returns a value. Values leave the store only through <see cref="ICloudSecretProvider"/>, which the control plane calls at provisioning.
/// </summary>
/// <remarks>
/// Encryption at rest is the operator's concern. <see cref="InMemoryCloudSecretStore"/> keeps values in process memory only and loses them on restart.
/// A persistent implementation must encrypt values with a key the operator holds (for example envelope encryption under a KMS key) and must not
/// log or return them. No persistent implementation ships: the encryption scheme is an operator decision (proposed OQ in the report).
/// </remarks>
internal interface ICloudSecretStore
{
    /// <summary>Stores or replaces a secret.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="name">The validated secret name.</param>
    /// <param name="value">The validated secret value.</param>
    /// <param name="maxSecrets">How many secrets the owner may have; a new name beyond this is refused.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The metadata of the stored secret, or null when the owner is at the cap and the name is new.</returns>
    Task<CloudSecretInfo?> SetAsync(string ownerId, string name, string value, int maxSecrets, CancellationToken cancellationToken);

    /// <summary>Reads a secret's metadata.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The metadata, or null when the owner has no such secret.</returns>
    Task<CloudSecretInfo?> GetInfoAsync(string ownerId, string name, CancellationToken cancellationToken);

    /// <summary>Lists an owner's secrets by name.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The metadata, ordered by name.</returns>
    Task<IReadOnlyList<CloudSecretInfo>> ListAsync(string ownerId, CancellationToken cancellationToken);

    /// <summary>Deletes a secret.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a secret was deleted.</returns>
    Task<bool> DeleteAsync(string ownerId, string name, CancellationToken cancellationToken);
}
