namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// The bounds the automation model enforces, in one place so the validator, the API and the editor agree.
/// </summary>
public static class AutomationLimits
{
    /// <summary>The longest automation name.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The longest prompt, in characters.</summary>
    public const int MaxPromptLength = 20_000;

    /// <summary>The most automations one owner may have.</summary>
    public const int MaxAutomationsPerOwner = 50;

    /// <summary>The most triggers one automation has.</summary>
    public const int MaxTriggers = 8;

    /// <summary>The most repositories a cloud target names.</summary>
    public const int MaxRepositories = 16;

    /// <summary>The most secret names a cloud target holds.</summary>
    public const int MaxSecretRefs = 32;

    /// <summary>The most entries in one filter list (events, actions, branches) or in the allowed tool list.</summary>
    public const int MaxListEntries = 16;

    /// <summary>The longest entry of a filter list, a branch name, an environment id, a model or a mode.</summary>
    public const int MaxEntryLength = 200;

    /// <summary>The default number of runs that may be active at once.</summary>
    public const int DefaultMaxConcurrentRuns = 1;

    /// <summary>The most runs that may be active at once.</summary>
    public const int MaxConcurrentRunsCeiling = 4;

    /// <summary>The default cap on runs that start in a rolling hour.</summary>
    public const int DefaultMaxRunsPerHour = 12;

    /// <summary>The highest cap on runs per hour. The run history keeps more entries than this, so the cap can always be counted.</summary>
    public const int MaxRunsPerHourCeiling = 60;

    /// <summary>The default longest run, in minutes.</summary>
    public const int DefaultMaxRuntimeMinutes = 30;

    /// <summary>The highest longest-run setting, in minutes.</summary>
    public const int MaxRuntimeMinutesCeiling = 360;

    /// <summary>How many runs the history keeps per automation.</summary>
    public const int MaxHistoryEntries = 100;

    /// <summary>The longest error text kept in a history entry.</summary>
    public const int MaxErrorLength = 500;

    /// <summary>How late a scheduled occurrence may run and still run once. A later one is skipped.</summary>
    public static readonly TimeSpan MisfireWindow = TimeSpan.FromMinutes(5);
}
