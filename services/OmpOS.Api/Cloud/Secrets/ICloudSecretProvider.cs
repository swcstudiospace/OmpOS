namespace OmpOS.Api.Cloud;

/// <summary>
/// The read path of secrets, used only by the control plane: at provisioning, to build the worker's environment, and at run time, to build the
/// redactor. Several providers may be registered; <see cref="CloudSecretResolver"/> asks them in registration order and the first answer wins.
/// </summary>
internal interface ICloudSecretProvider
{
    /// <summary>Looks up a secret value for an owner.</summary>
    /// <param name="ownerId">The owner of the agent that references the secret.</param>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The value, or null when this provider has no such secret.</returns>
    ValueTask<string?> TryGetValueAsync(string ownerId, string name, CancellationToken cancellationToken);

    /// <summary>Returns whether this provider has a secret of that name for the owner, without reading its value.</summary>
    /// <param name="ownerId">The owner.</param>
    /// <param name="name">The secret name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the secret exists.</returns>
    ValueTask<bool> ExistsAsync(string ownerId, string name, CancellationToken cancellationToken);
}
