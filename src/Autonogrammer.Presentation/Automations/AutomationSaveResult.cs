namespace Autonogrammer.Presentation.Automations;

/// <summary>
/// What saving an automation returns: the saved entry and, when saving generated a webhook secret, that secret. The secret is shown to
/// the user once. This is a class with no <see cref="object.ToString"/> override on purpose: a record would print the secret.
/// </summary>
public sealed class AutomationSaveResult
{
    /// <summary>Creates the result of a save.</summary>
    /// <param name="entry">The saved automation.</param>
    /// <param name="webhookSecret">The generated webhook secret, or null when saving generated none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public AutomationSaveResult(HostedAutomationEntry entry, string? webhookSecret)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
        WebhookSecret = webhookSecret;
    }

    /// <summary>Gets the saved automation.</summary>
    public HostedAutomationEntry Entry { get; }

    /// <summary>Gets the generated webhook secret, or null when saving generated none. It cannot be read again.</summary>
    public string? WebhookSecret { get; }
}
