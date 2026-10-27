namespace OmpOS.Contracts.Catalog;

/// <summary>The answer of <c>POST /sessions/{id}/close</c>, <c>reopen</c> and <c>resume</c>: the session's row after the operation.</summary>
public sealed record SessionLifecycleResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets a value indicating whether the session is closed after the operation.</summary>
    public required bool Closed { get; init; }

    /// <summary>Gets the status the rail shows for the session after the operation.</summary>
    public required CatalogRowStatus RowStatus { get; init; }

    /// <summary>Gets the rail group of the session after the operation.</summary>
    public required CatalogRowGroup Group { get; init; }
}
