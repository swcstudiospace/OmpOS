namespace OmpOS.Contracts.Automations;

/// <summary>The body of <c>GET /automations/definitions</c>: the caller's automations, newest first.</summary>
public sealed record AutomationListResponse
{
    /// <summary>Gets the automations.</summary>
    public required IReadOnlyList<AutomationInfo> Automations { get; init; }
}
