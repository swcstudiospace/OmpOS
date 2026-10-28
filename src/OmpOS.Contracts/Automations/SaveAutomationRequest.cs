namespace OmpOS.Contracts.Automations;

/// <summary>The body of <c>POST /automations</c> and <c>PUT /automations/{id}</c>. A PUT replaces the whole definition.</summary>
public sealed record SaveAutomationRequest
{
    /// <summary>Gets the display name, 1 to 100 characters.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the instructions each run starts with, 1 to 20000 characters.</summary>
    public string? Prompt { get; init; }

    /// <summary>Gets a value indicating whether triggers start runs. Default false: a new automation is created off.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Gets the triggers, 1 to 8.</summary>
    public IReadOnlyList<AutomationTriggerInfo>? Triggers { get; init; }

    /// <summary>Gets where the runs execute. Default: a cloud agent.</summary>
    public AutomationTargetInfo? Target { get; init; }

    /// <summary>Gets the tools a run may use: <c>pull_request_create</c>, <c>pull_request_comment</c>, <c>pull_request_request_reviewers</c>. When absent only <c>pull_request_create</c> is allowed.</summary>
    public IReadOnlyList<string>? AllowedTools { get; init; }

    /// <summary>Gets how many runs may be active at once, 1 to 4. Default 1.</summary>
    public int? MaxConcurrentRuns { get; init; }

    /// <summary>Gets how many runs may start in any rolling hour, 1 to 60. Default 12.</summary>
    public int? MaxRunsPerHour { get; init; }

    /// <summary>Gets the IANA time zone cron triggers are evaluated in. Default <c>UTC</c>.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Gets the longest a run may last, in minutes, 1 to 360. Default 30.</summary>
    public int? MaxRuntimeMinutes { get; init; }
}
