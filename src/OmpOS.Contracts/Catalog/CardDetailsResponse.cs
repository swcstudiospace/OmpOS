namespace OmpOS.Contracts.Catalog;

/// <summary>The answer of <c>GET /sessions/{id}/card-details</c>: what the substrate knows about the session behind a card.</summary>
public sealed record CardDetailsResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets a value indicating whether a task-context source answered. When false every other field is null and <see cref="Reason"/> says why.</summary>
    public required bool Available { get; init; }

    /// <summary>Gets a plain sentence that says why the details are unavailable, or null when they are available.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets the title of the task the session works on, or null when not reported.</summary>
    public string? TaskTitle { get; init; }

    /// <summary>Gets the Graph identifier of the session's plan, or null when not reported.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the number of lessons recorded for the task, or null when not reported.</summary>
    public int? LessonsCount { get; init; }

    /// <summary>Gets the holder of the task lease, or null when the source did not report one.</summary>
    public string? LeaseHolder { get; init; }
}
