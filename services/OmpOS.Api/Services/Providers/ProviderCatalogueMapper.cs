using OmpOS.Contracts.Providers;
using OmpOS.Harness.Providers;
using ProviderKind = OmpOS.Contracts.Providers.ProviderKind;

namespace OmpOS.Api.Services.Providers;

/// <summary>Maps the rows of the provider catalogue to the contract shapes. The mapping is one to one; a row holds no key, so neither does a summary.</summary>
internal static class ProviderCatalogueMapper
{
    /// <summary>Maps one row.</summary>
    /// <param name="row">The row.</param>
    /// <param name="configured">Whether the service has a key or sign-in for it.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public static ProviderSummary ToSummary(CatalogueRow row, ProviderConfiguredState configured)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new ProviderSummary
        {
            Id = row.Id,
            DisplayName = row.DisplayName,
            Region = ToRegion(row.Region),
            Country = row.Country,
            ResidencyNote = row.ResidencyNote,
            Kind = ToKind(row.Kind),
            Status = ToStatus(row.Status),
            StatusDate = row.StatusDate,
            OmpProviderId = row.OmpProviderId,
            ApiKeyEnv = row.ApiKeyEnv,
            TakesKey = row.Routes.Count > 0 && row.Routes.Any(static route => route.Auth != CatalogueAuthStyle.None),
            SignInOnly = row.Routes.Count == 0,
            LoginCommand = row.LoginCommand,
            Routes = [.. row.Routes.Select(ToRoute)],
            UrlVariables = [.. row.UrlVariables.Select(static variable => new ProviderUrlVariableInfo
            {
                Name = variable.Name,
                Label = variable.Label,
                Example = variable.Example,
                Help = variable.Help,
            })],
            DocsUrl = row.DocsUrl,
            KeyPageUrl = row.KeyPageUrl,
            SourceUrl = row.SourceUrl,
            Note = row.Note,
            Plan = row.Plan is null ? null : ToPlan(row.Plan),
            Configured = configured,
        };
    }

    private static ProviderRouteInfo ToRoute(CatalogueRoute route) => new()
    {
        Id = route.Id,
        Api = route.Api,
        BaseAddressTemplate = route.BaseAddressTemplate,
        Auth = route.Auth switch
        {
            CatalogueAuthStyle.Bearer => "Bearer",
            CatalogueAuthStyle.XApiKey => "x-api-key",
            CatalogueAuthStyle.CustomHeader => $"header {route.AuthHeader}",
            _ => "none",
        },
        TakesKey = route.Auth != CatalogueAuthStyle.None,
        DiscoversModels = route.Discovery,
        NeedsUserModels = route.NeedsUserModels,
        DocumentedModels = [.. route.Models.Select(static model => model.Id)],
    };

    private static ProviderPlanInfo ToPlan(CataloguePlanTerms plan) => new()
    {
        RuleText = plan.RuleText,
        RuleSourceUrl = plan.RuleSourceUrl,
        InteractiveOnly = plan.InteractiveOnly,
        SeparateKey = plan.SeparateKey,
        PlanUrl = plan.PlanUrl,
        Tiers = [.. plan.Tiers.Select(static tier => new ProviderPlanTierInfo
        {
            Name = tier.Name,
            PriceText = tier.PriceText,
            PriceVerified = tier.PriceVerified,
            QuotaText = tier.QuotaText,
        })],
        ListedTools = plan.ListedTools,
    };

    private static ProviderRegion ToRegion(CatalogueRegion region) => region switch
    {
        CatalogueRegion.Eu => ProviderRegion.Europe,
        CatalogueRegion.Us => ProviderRegion.UnitedStates,
        CatalogueRegion.Cn => ProviderRegion.China,
        CatalogueRegion.Local => ProviderRegion.Local,
        _ => ProviderRegion.Global,
    };

    private static ProviderKind ToKind(CatalogueKind kind) => kind switch
    {
        CatalogueKind.Aggregator => ProviderKind.Aggregator,
        CatalogueKind.CodingPlan => ProviderKind.CodingPlan,
        CatalogueKind.Local => ProviderKind.Local,
        _ => ProviderKind.Api,
    };

    private static ProviderVerification ToStatus(CatalogueStatus status) => status switch
    {
        CatalogueStatus.Verified => ProviderVerification.Verified,
        CatalogueStatus.Partial => ProviderVerification.Partial,
        _ => ProviderVerification.Unverified,
    };
}
