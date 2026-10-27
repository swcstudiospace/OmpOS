namespace OmpOS.Contracts.Catalog;

/// <summary>The data of a <c>resync</c> event: the stream cannot replay from the requested position, so the client reloads the catalog.</summary>
public sealed record CatalogResyncEvent
{
    /// <summary>Gets a plain sentence that says why the client must reload.</summary>
    public required string Reason { get; init; }
}
