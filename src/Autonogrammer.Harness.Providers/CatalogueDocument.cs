namespace Autonogrammer.Harness.Providers;

/// <summary>The shape of one catalogue data file: the rows it holds.</summary>
/// <param name="Rows">The rows.</param>
public sealed record CatalogueDocument(IReadOnlyList<CatalogueRow> Rows);
