using OmpOS.Harness.Automations.Cron;

namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Works out when an automation's cron triggers fire next.
/// </summary>
public static class AutomationNextRun
{
    /// <summary>Returns the earliest occurrence of any cron trigger strictly after an instant.</summary>
    /// <param name="automation">The automation.</param>
    /// <param name="after">The instant to start after.</param>
    /// <returns>The instant in UTC, or null when the automation has no valid cron trigger or none has an occurrence.</returns>
    public static DateTimeOffset? Next(UserAutomation automation, DateTimeOffset after)
    {
        ArgumentNullException.ThrowIfNull(automation);
        if (!AutomationTimeZones.TryResolve(automation.TimeZone, out var zone))
        {
            return null;
        }

        DateTimeOffset? best = null;
        foreach (var trigger in automation.Triggers)
        {
            if (trigger.Kind != AutomationTriggerKind.Cron || !CronExpression.TryParse(trigger.Cron, out var expression, out _))
            {
                continue;
            }

            if (expression.GetNextOccurrence(after, zone) is { } next && (best is null || next < best.Value))
            {
                best = next;
            }
        }

        return best;
    }
}
