using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Automations;

/// <summary>An automation as the API reports it. It never carries the webhook secret.</summary>
public sealed record AutomationInfo
{
    /// <summary>Gets the identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the instructions each run starts with.</summary>
    public required string Prompt { get; init; }

    /// <summary>Gets a value indicating whether triggers start runs.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Gets the triggers.</summary>
    public required IReadOnlyList<AutomationTriggerInfo> Triggers { get; init; }

    /// <summary>Gets where the runs execute.</summary>
    public required AutomationTargetInfo Target { get; init; }

    /// <summary>Gets the tools a run may use.</summary>
    public required IReadOnlyList<string> AllowedTools { get; init; }

    /// <summary>Gets how many runs may be active at once.</summary>
    public required int MaxConcurrentRuns { get; init; }

    /// <summary>Gets how many runs may start in any rolling hour.</summary>
    public required int MaxRunsPerHour { get; init; }

    /// <summary>Gets the time zone cron triggers are evaluated in.</summary>
    public required string TimeZone { get; init; }

    /// <summary>Gets the longest a run may last, in minutes.</summary>
    public required int MaxRuntimeMinutes { get; init; }

    /// <summary>Gets when the automation was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the automation was last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets when the next cron trigger fires, or null when the automation is off, has no cron trigger or none fires.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? NextRunAt { get; init; }

    /// <summary>Gets the most recent run, or null when it has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AutomationRunInfo? LastRun { get; init; }

    /// <summary>Gets the path of the webhook receiver, <c>/hooks/automations/{id}</c>, or null when the automation has no webhook trigger. Prefix it with the API origin.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WebhookPath { get; init; }

    /// <summary>Gets a value indicating whether a webhook secret is stored. The value is shown once, when it is created or rotated.</summary>
    public required bool HasWebhookSecret { get; init; }
}
