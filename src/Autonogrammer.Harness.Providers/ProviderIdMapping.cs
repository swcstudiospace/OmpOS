namespace Autonogrammer.Harness.Providers;

/// <summary>Where a legacy <see cref="ProviderCatalogue"/> entry id lives in the data-driven catalogue, and which id omp knows it by.</summary>
/// <param name="LegacyId">The id of the <see cref="ProviderEntry"/> in <see cref="ProviderCatalogue"/>.</param>
/// <param name="RowId">The id of the <see cref="CatalogueRow"/> that carries the same provider.</param>
/// <param name="RouteId">The id of the route of that row that reaches the same endpoint.</param>
/// <param name="OmpProviderId">The id omp's own provider list uses for the provider, or null when omp has no native provider for this host and key kind.</param>
public sealed record ProviderIdMapping(string LegacyId, string RowId, string RouteId, string? OmpProviderId);
