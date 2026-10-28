namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Keeps the secrets of automations, for now the webhook signing secrets, under references such as
/// <see cref="AutomationSecrets.WebhookReference"/>. A secret value is write-only for everything except the signature check: no
/// listing, response or log carries it.
/// </summary>
public interface IAutomationSecretStore
{
    /// <summary>Reads a secret value.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The value, or null when no secret has the reference.</returns>
    ValueTask<string?> GetAsync(string reference, CancellationToken cancellationToken);

    /// <summary>Stores or replaces a secret value.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    ValueTask SetAsync(string reference, string value, CancellationToken cancellationToken);

    /// <summary>Deletes a secret. Nothing happens when no secret has the reference.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the secret is gone.</returns>
    ValueTask DeleteAsync(string reference, CancellationToken cancellationToken);
}
