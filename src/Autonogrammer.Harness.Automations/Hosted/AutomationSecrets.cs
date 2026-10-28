namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The references under which an automation's secrets are kept in an <see cref="IAutomationSecretStore"/>.
/// </summary>
public static class AutomationSecrets
{
    /// <summary>Returns the reference of an automation's webhook signing secret.</summary>
    /// <param name="automationId">The automation identifier.</param>
    /// <returns>The reference, <c>webhook:</c> followed by the identifier.</returns>
    public static string WebhookReference(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        return "webhook:" + automationId;
    }
}
