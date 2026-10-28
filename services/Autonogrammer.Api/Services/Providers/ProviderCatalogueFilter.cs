using Autonogrammer.Contracts.Providers;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>The filters of <c>GET /providers</c>. A null member does not filter.</summary>
/// <param name="Region">Keeps the providers of one region.</param>
/// <param name="Kind">Keeps the providers of one kind.</param>
/// <param name="Search">Keeps the providers whose id, name, country, note or route address holds the text, ignoring case.</param>
internal sealed record ProviderCatalogueFilter(ProviderRegion? Region, ProviderKind? Kind, string? Search)
{
    /// <summary>The longest search text accepted.</summary>
    public const int MaxSearchLength = 100;

    /// <summary>Parses a region filter by its wire name.</summary>
    /// <param name="text">The query value.</param>
    /// <param name="region">The region.</param>
    /// <returns>True when the text names a region.</returns>
    public static bool TryParseRegion(string text, out ProviderRegion region)
    {
        ArgumentNullException.ThrowIfNull(text);
        (var found, region) = text.ToUpperInvariant() switch
        {
            "EUROPE" => (true, ProviderRegion.Europe),
            "UNITED-STATES" => (true, ProviderRegion.UnitedStates),
            "CHINA" => (true, ProviderRegion.China),
            "GLOBAL" => (true, ProviderRegion.Global),
            "LOCAL" => (true, ProviderRegion.Local),
            _ => (false, default(ProviderRegion)),
        };
        return found;
    }

    /// <summary>Parses a kind filter by its wire name.</summary>
    /// <param name="text">The query value.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>True when the text names a kind.</returns>
    public static bool TryParseKind(string text, out ProviderKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        (var found, kind) = text.ToUpperInvariant() switch
        {
            "API" => (true, ProviderKind.Api),
            "AGGREGATOR" => (true, ProviderKind.Aggregator),
            "CODING-PLAN" => (true, ProviderKind.CodingPlan),
            "LOCAL" => (true, ProviderKind.Local),
            _ => (false, default(ProviderKind)),
        };
        return found;
    }

    /// <summary>Tells whether a summary passes the filters.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>True when it is kept.</returns>
    public bool Keeps(ProviderSummary summary)
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
