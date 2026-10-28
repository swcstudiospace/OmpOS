namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// What started one run, as the run's prompt and history see it. Everything in <see cref="Payload"/> comes from outside Autonogrammer and is
/// untrusted: the prompt builder frames it as data.
/// </summary>
public sealed record AutomationTriggerContext
{
    /// <summary>Gets what started the run.</summary>
    public required AutomationRunSource Source { get; init; }

    /// <summary>Gets one line that says what happened, for example <c>push to main by octocat</c>. Shown in the run history.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Gets bounded text taken from the trigger, for example a summary of a GitHub event. Untrusted.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>Gets the instant a cron occurrence was due, or null for other sources.</summary>
    public DateTimeOffset? ScheduledFor { get; init; }

    /// <summary>Creates the context of a manual run.</summary>
    /// <returns>The context.</returns>
    public static AutomationTriggerContext Manual() => new() { Source = AutomationRunSource.Manual, Summary = "Run now" };

    /// <summary>Creates the context of a cron run.</summary>
    /// <param name="scheduledFor">The occurrence that came due.</param>
    /// <returns>The context.</returns>
    public static AutomationTriggerContext Cron(DateTimeOffset scheduledFor) =>
        new() { Source = AutomationRunSource.Cron, Summary = "Scheduled run", ScheduledFor = scheduledFor };
}
