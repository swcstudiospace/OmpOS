using OmpOS.Harness.Providers;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Settings.Models;

/// <summary>
/// Maps the rows of <see cref="CatalogueRows"/> to the listings of the Models page. The mapping is one to one: nothing is added or
/// dropped, and a row holds no key, so neither does a listing. Whether a listing is set up is not decided here; the page matches the
/// listing to the providers omp reports.
/// </summary>
internal static class CatalogueListingMapper
{
    /// <summary>Maps every catalogue row, in the order the catalogue lists them.</summary>
    /// <returns>The listings.</returns>
    public static IReadOnlyList<ProviderListing> FromCatalogue() => [.. CatalogueRows.All.Select(ToListing)];

    /// <summary>Maps one catalogue row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The listing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public static ProviderListing ToListing(CatalogueRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new ProviderListing(
            row.Id,
            row.DisplayName,
            ToRegion(row.Region),
            row.Country,
            row.ResidencyNote,
            ToKind(row.Kind),
            row.OmpProviderId,
            row.ApiKeyEnv,
            row.LoginCommand,
            [.. row.Routes.Select(ToRoute)],
            [.. row.UrlVariables.Select(static variable => new ProviderListingVariable(variable.Name, variable.Label, variable.Example, variable.Help))],
            row.DocsUrl,
            row.KeyPageUrl,
            row.SourceUrl,
            ToStatus(row.Status),
            row.StatusDate,
            row.Note,
            row.Plan is null ? null : ToPlan(row.Plan));
    }

    private static ProviderListingRoute ToRoute(CatalogueRoute route) =>
        new(
            route.Id,
            route.Api,
            route.BaseAddressTemplate,
            AuthText(route),
            route.Auth != CatalogueAuthStyle.None,
            route.Discovery,
            route.NeedsUserModels,
            [.. route.Models.Select(static model => model.Id)]);

    private static string AuthText(CatalogueRoute route) => route.Auth switch
    {
        CatalogueAuthStyle.Bearer => "Bearer",
        CatalogueAuthStyle.XApiKey => "x-api-key",
        CatalogueAuthStyle.CustomHeader => $"header {route.AuthHeader}",
        _ => "none",
    };

    private static ProviderListingPlan ToPlan(CataloguePlanTerms plan) =>
        new(
            plan.RuleText,
            plan.RuleSourceUrl,
            plan.InteractiveOnly,
            plan.SeparateKey,
            plan.PlanUrl,
            [.. plan.Tiers.Select(static tier => new ProviderListingTier(tier.Name, tier.PriceText, tier.PriceVerified, tier.QuotaText))],
            plan.ListedTools);

    private static ProviderListingRegion ToRegion(CatalogueRegion region) => region switch
    {
        CatalogueRegion.Eu => ProviderListingRegion.Europe,
        CatalogueRegion.Us => ProviderListingRegion.UnitedStates,
        CatalogueRegion.Cn => ProviderListingRegion.China,
        CatalogueRegion.Local => ProviderListingRegion.Local,
        _ => ProviderListingRegion.Global,
    };

    private static ProviderListingKind ToKind(CatalogueKind kind) => kind switch
    {
        CatalogueKind.Aggregator => ProviderListingKind.Aggregator,
        CatalogueKind.CodingPlan => ProviderListingKind.CodingPlan,
        CatalogueKind.Local => ProviderListingKind.Local,
        _ => ProviderListingKind.Api,
    };

    private static ProviderListingStatus ToStatus(CatalogueStatus status) => status switch
    {
        CatalogueStatus.Verified => ProviderListingStatus.Verified,
        CatalogueStatus.Partial => ProviderListingStatus.Partial,
        _ => ProviderListingStatus.Unverified,
    };
}
