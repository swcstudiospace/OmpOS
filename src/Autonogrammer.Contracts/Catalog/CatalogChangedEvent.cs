namespace Autonogrammer.Contracts.Catalog;

/// <summary>The data of a <c>catalog_changed</c> event: which session changed and how.</summary>
public sealed record CatalogChangedEvent
{
    /// <summary>Gets the identifier of the session that changed.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets how the catalog changed.</summary>
    public required CatalogChangeKind Change { get; init; }

    /// <summary>Gets the status the rail shows for the session after the change.</summary>
    public required CatalogRowStatus RowStatus { get; init; }

    /// <summary>Gets the rail group of the session after the change.</summary>
    public required CatalogRowGroup Group { get; init; }
}
