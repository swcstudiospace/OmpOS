namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The fields a caller supplies to create or replace an automation. The service assigns the identifier, the owner and the times.
/// </summary>
public sealed record AutomationDraft
{
    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the instructions each run starts with.</summary>
    public required string Prompt { get; init; }

    /// <summary>Gets whether triggers start runs.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the triggers.</summary>
    public IReadOnlyList<AutomationTriggerSpec> Triggers { get; init; } = [];

    /// <summary>Gets where the runs execute.</summary>
    public AutomationTargetSpec Target { get; init; } = new();

    /// <summary>Gets the tools a run may use. When null the default (<see cref="AutomationTools.Default"/>) applies.</summary>
    public IReadOnlyList<string>? AllowedTools { get; init; }

    /// <summary>Gets how many runs may be active at once.</summary>
    public int MaxConcurrentRuns { get; init; } = AutomationLimits.DefaultMaxConcurrentRuns;

    /// <summary>Gets how many runs may start in any rolling hour.</summary>
    public int MaxRunsPerHour { get; init; } = AutomationLimits.DefaultMaxRunsPerHour;

    /// <summary>Gets the time zone cron triggers are evaluated in.</summary>
    public string TimeZone { get; init; } = "UTC";

    /// <summary>Gets the longest a run may last, in minutes.</summary>
    public int MaxRuntimeMinutes { get; init; } = AutomationLimits.DefaultMaxRuntimeMinutes;
}
