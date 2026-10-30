namespace Autonogrammer.Contracts.Catalog;

/// <summary>One task card of the board: one session.</summary>
public sealed record BoardCard
{
    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the card title. It is derived from the session identifier, never from a prompt.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the status the rail shows for the session.</summary>
    public required CatalogRowStatus RowStatus { get; init; }

    /// <summary>Gets the key of the column the card sits in.</summary>
    public required string ColumnKey { get; init; }

    /// <summary>Gets the time of the last activity of the session.</summary>
    public required DateTimeOffset LastActivityAt { get; init; }

    /// <summary>Gets a value indicating whether the session shows as running but has had no activity for longer than a run lease lives.</summary>
    public required bool IsStale { get; init; }

    /// <summary>Gets a value indicating whether the operator closed the session.</summary>
    public required bool Closed { get; init; }
}
