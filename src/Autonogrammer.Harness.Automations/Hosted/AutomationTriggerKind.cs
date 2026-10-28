namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// What starts an automation. A manual run is always possible and is not a trigger.
/// </summary>
public enum AutomationTriggerKind
{
    /// <summary>A cron schedule.</summary>
    Cron,

    /// <summary>A signed HTTP POST to the automation's webhook URL.</summary>
    Webhook,
}
