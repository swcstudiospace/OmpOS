namespace OmpOS.Contracts.Automations;

/// <summary>The body of <c>GET /automations/{id}/runs</c>: the run history, newest first, at most 100.</summary>
public sealed record AutomationRunListResponse
{
    /// <summary>Gets the runs.</summary>
    public required IReadOnlyList<AutomationRunInfo> Runs { get; init; }
}
