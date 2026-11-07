namespace Autonogrammer.Harness.Automations.Scheduling;

/// <summary>
/// What a session task does with occurrences the desktop missed while it was closed. Non-session targets keep today's
/// five-minute misfire rule and do not read this value.
/// </summary>
public enum MissedRunPolicy
{
    /// <summary>One late run for the newest occurrence inside the catch-up window. Older than that window is skipped.</summary>
    RunOnce,

    /// <summary>Today's rule: an occurrence later than the five-minute misfire window is skipped.</summary>
    Skip,
}
