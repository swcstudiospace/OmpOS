namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// What started a run.
/// </summary>
public enum AutomationRunSource
{
    /// <summary>A person asked for the run (Run now).</summary>
    Manual,

    /// <summary>A cron schedule came due.</summary>
    Cron,

    /// <summary>A verified webhook delivery arrived.</summary>
    Webhook,
}
