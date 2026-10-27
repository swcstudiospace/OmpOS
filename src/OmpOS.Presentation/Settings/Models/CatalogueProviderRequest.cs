namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// A request to add a catalogue provider to omp: which listing and route, the values of its base URL placeholders and the model ids
/// the user added. The key travels apart, as a separate argument of <see cref="IModelsSettingsSource.AddCatalogueProviderAsync"/>, so
/// this record can be logged.
/// </summary>
/// <param name="ListingId">The <see cref="ProviderListing.Id"/> to add.</param>
/// <param name="RouteId">The <see cref="ProviderListingRoute.Id"/> to use.</param>
/// <param name="UrlValues">The value of each base URL placeholder by name.</param>
/// <param name="ModelIds">The model ids the user added; empty when the route discovers models.</param>
public sealed record CatalogueProviderRequest(
    string ListingId,
    string RouteId,
    IReadOnlyDictionary<string, string> UrlValues,
    IReadOnlyList<string> ModelIds);
