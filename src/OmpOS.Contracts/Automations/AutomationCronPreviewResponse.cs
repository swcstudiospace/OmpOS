using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Automations;

/// <summary>The body of <c>GET /automations/cron-preview</c>: the next occurrences of a cron expression.</summary>
public sealed record AutomationCronPreviewResponse
{
    /// <summary>Gets the next occurrences, in UTC, soonest first. Empty when the expression is invalid or never fires.</summary>
    public required IReadOnlyList<DateTimeOffset> NextRuns { get; init; }

    /// <summary>Gets why the expression or the time zone is invalid, or null when both are valid.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}
