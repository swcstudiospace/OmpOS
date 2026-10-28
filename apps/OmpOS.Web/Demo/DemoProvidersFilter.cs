using OmpOS.Contracts.Providers;

namespace OmpOS.Web.Demo;

/// <summary>The <c>region</c>, <c>kind</c> and <c>search</c> filters of the Demo providers route, with the wire names and the matching of the API.</summary>
internal sealed class DemoProvidersFilter
{
    private DemoProvidersFilter(ProviderRegion? region, ProviderKind? kind, string? search)
    {
        Region = region;
        Kind = kind;
        Search = search;
    }

    /// <summary>Gets the region to keep, or null for every region.</summary>
    internal ProviderRegion? Region { get; }

    /// <summary>Gets the kind to keep, or null for every kind.</summary>
    internal ProviderKind? Kind { get; }

    /// <summary>Gets the search text, or null.</summary>
    internal string? Search { get; }

    /// <summary>Parses the query values of a request.</summary>
    /// <param name="region">The <c>region</c> value, or null.</param>
    /// <param name="kind">The <c>kind</c> value, or null.</param>
    /// <param name="search">The <c>search</c> value, or null.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="DemoProblemException">The region or kind is not one of the wire names (400 <c>invalid_filter</c>).</exception>
    internal static DemoProvidersFilter Parse(string? region, string? kind, string? search)
    {
        ProviderRegion? parsedRegion = null;
        if (!string.IsNullOrWhiteSpace(region))
        {
            parsedRegion = region.Trim().ToUpperInvariant() switch
            {
                "EUROPE" => ProviderRegion.Europe,
                "UNITED-STATES" => ProviderRegion.UnitedStates,
                "CHINA" => ProviderRegion.China,
                "GLOBAL" => ProviderRegion.Global,
                "LOCAL" => ProviderRegion.Local,
                _ => throw new DemoProblemException(400, "invalid_filter", "Invalid filter.", "The region must be one of europe, united-states, china, global or local."),
            };
        }

        ProviderKind? parsedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            parsedKind = kind.Trim().ToUpperInvariant() switch
            {
                "API" => ProviderKind.Api,
                "AGGREGATOR" => ProviderKind.Aggregator,
                "CODING-PLAN" => ProviderKind.CodingPlan,
                "LOCAL" => ProviderKind.Local,
                _ => throw new DemoProblemException(400, "invalid_filter", "Invalid filter.", "The kind must be one of api, aggregator, coding-plan or local."),
            };
        }

        return new DemoProvidersFilter(parsedRegion, parsedKind, search);
    }

    /// <summary>Tells whether a summary passes the filter.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>True when it is kept.</returns>
    internal bool Keeps(ProviderSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (Region is { } region && summary.Region != region)
        {
            return false;
        }

        if (Kind is { } kind && summary.Kind != kind)
        {
            return false;
        }

        var text = Search?.Trim();
        return string.IsNullOrEmpty(text)
            || Contains(summary.Id, text)
            || Contains(summary.DisplayName, text)
            || Contains(summary.Country, text)
            || Contains(summary.Note, text)
            || summary.Routes.Any(route => Contains(route.BaseAddressTemplate, text) || Contains(route.Api, text));
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
