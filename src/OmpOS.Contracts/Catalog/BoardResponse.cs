namespace OmpOS.Contracts.Catalog;

/// <summary>The answer of <c>GET /board</c>: the columns and cards built from the catalog.</summary>
public sealed record BoardResponse
{
    /// <summary>Gets the board state.</summary>
    public required BoardStateKind State { get; init; }

    /// <summary>Gets the columns in display order. Every column is present, empty ones with no cards.</summary>
    public required IReadOnlyList<BoardColumn> Columns { get; init; }
}
