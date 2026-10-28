using System.Text.Json.Serialization;

namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// One entry of an automation's run history: when a run started, what started it, how it stands or ended, and the error if it failed.
/// The error text is bounded and never holds a secret.
/// </summary>
public sealed record AutomationRunEntry
{
    /// <summary>Gets the run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets what started the run.</summary>
    public required AutomationRunSource Source { get; init; }

    /// <summary>Gets the one-line summary of the trigger.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Gets how the run stands or ended.</summary>
    public required AutomationRunStatus Status { get; init; }

    /// <summary>Gets when the run started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets when the run ended, or null while it is running.</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Gets the failure text of a failed or timed out run, bounded to 500 characters.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the identifier of what the target created, for example the cloud agent, or null.</summary>
    public string? ExternalId { get; init; }

    /// <summary>Gets how long the run lasted, or null while it is running.</summary>
    [JsonIgnore]
    public TimeSpan? Duration => FinishedAt - StartedAt;

    /// <summary>Gets whether the run counts against the hourly cap: it was admitted and not merely skipped.</summary>
    [JsonIgnore]
    public bool CountsAgainstHourlyCap => Status != AutomationRunStatus.Skipped;
}
