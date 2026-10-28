namespace OmpOS.Contracts.Automations;

/// <summary>One run of an automation.</summary>
public sealed record AutomationRunInfo
{
    /// <summary>Gets the run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets what started the run: <c>manual</c>, <c>cron</c> or <c>webhook</c>.</summary>
    public required string Trigger { get; init; }

    /// <summary>Gets one line that says what happened, for example <c>push to main by octocat (3 commits)</c>. Taken from outside OmpOS for webhook runs: show it as text.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets how the run stands or ended: <c>running</c>, <c>succeeded</c>, <c>launched</c>, <c>failed</c>, <c>timed_out</c>, <c>cancelled</c> or <c>skipped</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when the run started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets when the run ended, or null while it runs.</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Gets how long the run lasted, in seconds, or null while it runs.</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>Gets why a run failed, timed out or was skipped. Never holds a secret.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the identifier of what the run created, for example the cloud agent (see <c>GET /cloud/agents/{id}</c>).</summary>
    public string? ExternalId { get; init; }
}
