namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>The latest run attempt of an automation.</summary>
public sealed record AutomationLastRunInfo
{
    /// <summary>Gets how the attempt ended: <c>succeeded</c>, <c>failed</c>, <c>timed-out</c>, <c>cancelled</c>, <c>blocked</c> or <c>refused</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary>Gets a human-readable reason, or an empty string.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets when the attempt was recorded.</summary>
    public required DateTimeOffset RecordedAt { get; init; }
}
