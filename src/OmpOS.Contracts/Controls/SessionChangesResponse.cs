namespace OmpOS.Contracts.Controls;

/// <summary>The answer of <c>GET /sessions/{id}/changes</c>.</summary>
public sealed record SessionChangesResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets a value indicating whether the API has a workspace folder to apply a decision in. When false, a decision is recorded only.</summary>
    public required bool WorkspaceAvailable { get; init; }

    /// <summary>Gets the changes, in the order of their first edit.</summary>
    public required IReadOnlyList<SessionChange> Changes { get; init; }
}
