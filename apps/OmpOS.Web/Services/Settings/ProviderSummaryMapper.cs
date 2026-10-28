using OmpOS.Presentation.Settings.Models;
using Wire = OmpOS.Contracts.Providers;

namespace OmpOS.Web.Services.Settings;

/// <summary>Maps the provider shapes of the API to the shapes of the Models page. A summary holds no key, so neither does the result.</summary>
internal static class ProviderSummaryMapper
{
    /// <summary>Maps a catalogue summary to a listing.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The listing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> is null.</exception>
    public static ProviderListing ToListing(Wire.ProviderSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new ProviderListing(
            summary.Id,
            summary.DisplayName,
            ToRegion(summary.Region),
            summary.Country,
            summary.ResidencyNote,
            ToKind(summary.Kind),
            summary.OmpProviderId,
            summary.ApiKeyEnv,
            summary.LoginCommand,
            [.. summary.Routes.Select(static route => new ProviderListingRoute(
                route.Id,
                route.Api,
                route.BaseAddressTemplate,
                route.Auth,
                route.TakesKey,
                route.DiscoversModels,
                route.NeedsUserModels,
                route.DocumentedModels))],
            [.. summary.UrlVariables.Select(static variable => new ProviderListingVariable(variable.Name, variable.Label, variable.Example, variable.Help))],
            summary.DocsUrl,
            summary.KeyPageUrl,
            summary.SourceUrl,
            ToStatus(summary.Status),
            summary.StatusDate,
            summary.Note,
            summary.Plan is null ? null : ToPlan(summary.Plan));
    }

    /// <summary>Maps a configured catalogue summary to the provider card omp would report: the id is the omp id when omp knows the provider, so the listing finds it.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The card, or null when the service has no key for the provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> is null.</exception>
    public static ProviderInfo? ToConfiguredCard(Wire.ProviderSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.Configured != Wire.ProviderConfiguredState.Configured)
        {
            return null;
        }

        return new ProviderInfo(
            summary.OmpProviderId ?? summary.Id,
            summary.DisplayName,
            summary.Kind switch
            {
                Wire.ProviderKind.Local => ProviderGroup.Local,
                Wire.ProviderKind.CodingPlan => ProviderGroup.CodingPlan,
                _ => ProviderGroup.Cloud,
            },
            summary.Routes.Count > 0 ? summary.Routes[0].BaseAddressTemplate : string.Empty,
            summary.SignInOnly ? ProviderAuthKind.OAuth : ProviderAuthKind.ApiKey,
            CredentialState.Present,
            [],
            summary.KeyPageUrl,
            RetrievedOn: summary.StatusDate,
            Plan: null,
            IsOmpOSManaged: false,
            Note: null);
    }

    /// <summary>Maps a custom endpoint to a provider card with a definition, so the page can edit it.</summary>
    /// <param name="custom">The endpoint.</param>
    /// <returns>The card.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="custom"/> is null.</exception>
    public static ProviderInfo ToCustomCard(Wire.CustomProviderInfo custom)
    {
        ArgumentNullException.ThrowIfNull(custom);
        var keyed = custom.KeyEnvName is not null;
        var form = new CustomProviderForm(
            custom.DisplayName,
            custom.Id,
            custom.Protocol == Wire.CustomProviderProtocol.Anthropic ? CustomProviderProtocol.AnthropicCompatible : CustomProviderProtocol.OpenAiCompatible,
            new Uri(custom.BaseAddress, UriKind.Absolute),
            keyed ? CustomProviderAuth.ApiKey : CustomProviderAuth.None,
            custom.ModelIds,
            CustomProviderCapabilities.Chat,
            AllowRemotePlainHttp: false);
        return new ProviderInfo(
            custom.Id,
            custom.DisplayName,
            ProviderGroup.Custom,
            custom.BaseAddress,
            keyed ? ProviderAuthKind.ApiKey : ProviderAuthKind.None,
            custom.Configured switch
            {
                Wire.ProviderConfiguredState.Configured => CredentialState.Present,
                Wire.ProviderConfiguredState.NotConfigured => CredentialState.Absent,
                Wire.ProviderConfiguredState.NotRequired => CredentialState.NotRequired,
                _ => CredentialState.Unknown,
            },
            [.. custom.ModelIds.Select(model => custom.Id + "/" + model)],
            SourceUrl: null,
            RetrievedOn: null,
            Plan: null,
            IsOmpOSManaged: true,
            Note: null,
            form);
    }

    private static ProviderListingPlan ToPlan(Wire.ProviderPlanInfo plan) =>
        new(
            plan.RuleText,
            plan.RuleSourceUrl,
            plan.InteractiveOnly,
            plan.SeparateKey,
            plan.PlanUrl,
            [.. plan.Tiers.Select(static tier => new ProviderListingTier(tier.Name, tier.PriceText, tier.PriceVerified, tier.QuotaText))],
            plan.ListedTools);

    private static ProviderListingRegion ToRegion(Wire.ProviderRegion region) => region switch
    {
        Wire.ProviderRegion.Europe => ProviderListingRegion.Europe,
        Wire.ProviderRegion.UnitedStates => ProviderListingRegion.UnitedStates,
        Wire.ProviderRegion.China => ProviderListingRegion.China,
        Wire.ProviderRegion.Local => ProviderListingRegion.Local,
        _ => ProviderListingRegion.Global,
    };

    private static ProviderListingKind ToKind(Wire.ProviderKind kind) => kind switch
    {
        Wire.ProviderKind.Aggregator => ProviderListingKind.Aggregator,
        Wire.ProviderKind.CodingPlan => ProviderListingKind.CodingPlan,
        Wire.ProviderKind.Local => ProviderListingKind.Local,
        _ => ProviderListingKind.Api,
    };

    private static ProviderListingStatus ToStatus(Wire.ProviderVerification status) => status switch
    {
        Wire.ProviderVerification.Verified => ProviderListingStatus.Verified,
        Wire.ProviderVerification.Partial => ProviderListingStatus.Partial,
        _ => ProviderListingStatus.Unverified,
    };
}
