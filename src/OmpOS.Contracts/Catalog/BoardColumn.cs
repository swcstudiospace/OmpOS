namespace OmpOS.Contracts.Catalog;

/// <summary>One column of the board.</summary>
public sealed record BoardColumn
{
    /// <summary>Gets the stable column key: <c>backlog</c>, <c>queued</c>, <c>running</c>, <c>awaiting-input</c>, <c>needs-attention</c>, <c>paused</c>, <c>completed</c>, <c>failed</c>, <c>unreachable</c> or <c>archived</c>.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the column title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets a value indicating whether a dragged card may be dropped here: true only for Backlog and Archived.</summary>
    public required bool AcceptsDrop { get; init; }

    /// <summary>Gets the cards of the column, most recent activity first.</summary>
    public required IReadOnlyList<BoardCard> Cards { get; init; }
}
