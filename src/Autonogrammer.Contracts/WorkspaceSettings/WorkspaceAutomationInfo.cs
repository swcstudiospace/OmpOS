using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>One automation.</summary>
public sealed record WorkspaceAutomationInfo
{
    /// <summary>Gets the automation identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets what starts the automation, in words.</summary>
    public required string Trigger { get; init; }

    /// <summary>Gets a value indicating whether the automation is enabled.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Gets the longest a single run may last, in seconds.</summary>
    public required double MaxRuntimeSeconds { get; init; }

    /// <summary>Gets the latest run attempt, or null when there is none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AutomationLastRunInfo? LastRun { get; init; }

    /// <summary>Gets the pause a guard placed on the automation, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AutomationPauseInfo? Pause { get; init; }
}
