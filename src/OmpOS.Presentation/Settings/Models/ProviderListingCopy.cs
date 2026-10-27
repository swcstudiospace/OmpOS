using System.Collections.Frozen;
using System.Globalization;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// The words of the provider listing cards and filters on the Models page. Heads show these strings and never type their own. No
/// member takes a key as an argument.
/// </summary>
public static class ProviderListingCopy
{
    /// <summary>The heading of the provider listing section.</summary>
    public const string SectionTitle = "Providers and coding plans";

    /// <summary>The sentence under the heading of the provider listing section.</summary>
    public const string SectionDescription =
        "Every model provider, aggregator, coding plan and local runtime OmpOS knows, grouped by region and kind. Pick one, add its key and omp can use it.";

    /// <summary>The label of the region filter.</summary>
    public const string RegionFilterLabel = "Filter by region";

    /// <summary>The label of the kind filter.</summary>
    public const string KindFilterLabel = "Filter by kind";

    /// <summary>The first choice of both filters.</summary>
    public const string AllChoice = "All";

    /// <summary>The mark of a listing no usable vendor page backs.</summary>
    public const string VerifyBeforeUse = "Verify before use";

    /// <summary>The mark of a listing some of whose facts are not from a vendor page.</summary>
    public const string PartlyVerified = "Partly verified";

    /// <summary>The tag of a plan the vendor limits to interactive coding tools.</summary>
    public const string InteractiveOnlyTag = "Interactive use only";

    /// <summary>The tag of a plan whose key cannot be used for the vendor's pay-as-you-go API.</summary>
    public const string SeparateKeyTag = "Plan key only, not the pay-as-you-go key";

    /// <summary>The sentence every coding plan card carries.</summary>
    public const string CloudAgentsSentence = "Cloud agents use pay-as-you-go keys";

    /// <summary>The sentence that says OmpOS sends a plan key only to the plan's own host.</summary>
    public const string PlanHostSentence = "OmpOS sends this key only to the plan's own host.";

    /// <summary>The sentence under the model id field of a route that has no models list OmpOS can read.</summary>
    public const string ModelIdsNeededText = "This provider has no models list OmpOS can read. Enter at least one model id, separated by commas or spaces.";

    /// <summary>The sentence under the model id field of a route that discovers its models.</summary>
    public const string ModelIdsOptionalText = "omp reads the models from the provider. Add model ids here only to pin extra ones.";

    /// <summary>The sentence shown when the head cannot add catalogue providers.</summary>
    public const string NotSupportedText = "This version of OmpOS cannot add catalogue providers yet. Add the provider as a custom provider instead.";

    /// <summary>The accessible name of the live region of the provider listing.</summary>
    public const string AnnouncementName = "Provider listing changes";

    /// <summary>The label of the model id field.</summary>
    public const string ModelIdsLabel = "Model ids";

    /// <summary>The label of the route choice.</summary>
    public const string RouteLabel = "Protocol and endpoint";

    /// <summary>The sentence shown when no listing matches the filters.</summary>
    public const string NoMatchesText = "No provider matches the search and filters.";

    /// <summary>The text of the action that opens the setup form of a listing.</summary>
    public const string SetupActionLabel = "Set up";

    /// <summary>The text of the action that closes the setup form.</summary>
    public const string CancelActionLabel = "Cancel";

    /// <summary>The text of the action that adds the provider.</summary>
    public const string AddActionLabel = "Add provider";

    /// <summary>The text of the action that opens the vendor documentation.</summary>
    public const string DocsActionLabel = "Documentation";

    /// <summary>The text of the action that opens the page where a key is created.</summary>
    public const string KeyPageActionLabel = "Get a key";

    /// <summary>The text of the action that opens the plan page.</summary>
    public const string PlanPageActionLabel = "Plan page";

    private static readonly FrozenDictionary<string, string> Countries = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CA"] = "Canada",
        ["CH"] = "Switzerland",
        ["CN"] = "China",
        ["DE"] = "Germany",
        ["FR"] = "France",
        ["GB"] = "United Kingdom",
        ["IT"] = "Italy",
        ["LU"] = "Luxembourg",
        ["NL"] = "Netherlands",
        ["SE"] = "Sweden",
        ["US"] = "United States",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Returns the heading of a region.</summary>
    /// <param name="region">The region.</param>
    /// <returns>The heading.</returns>
    public static string RegionTitle(ProviderListingRegion region) => region switch
    {
        ProviderListingRegion.Europe => "Europe",
        ProviderListingRegion.UnitedStates => "United States and Canada",
        ProviderListingRegion.China => "China",
        ProviderListingRegion.Global => "Global",
        _ => "Local runtimes",
    };

    /// <summary>Returns the heading of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The heading.</returns>
    public static string KindTitle(ProviderListingKind kind) => kind switch
    {
        ProviderListingKind.Api => "Model APIs",
        ProviderListingKind.Aggregator => "Aggregators and gateways",
        ProviderListingKind.CodingPlan => "Coding plans",
        _ => "Local runtimes",
    };

    /// <summary>Returns the heading of one section: a region and a kind.</summary>
    /// <param name="region">The region.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The heading.</returns>
    public static string SectionHeading(ProviderListingRegion region, ProviderListingKind kind) =>
        region == ProviderListingRegion.Local ? RegionTitle(region) : $"{RegionTitle(region)}: {KindTitle(kind)}";

    /// <summary>Returns a filter choice with its count.</summary>
    /// <param name="title">The choice.</param>
    /// <param name="count">How many listings the choice keeps.</param>
    /// <returns>The text.</returns>
    public static string ChoiceText(string title, int count) => $"{title} ({count.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>Returns the name of a country, or the code itself when the table does not know it.</summary>
    /// <param name="code">The ISO 3166-1 alpha-2 code.</param>
    /// <returns>The name.</returns>
    public static string CountryName(string code) => Countries.GetValueOrDefault(code, code);

    /// <summary>Returns the line that names the status of a listing and the date it was established.</summary>
    /// <param name="status">The status.</param>
    /// <param name="date">The date.</param>
    /// <returns>The line.</returns>
    public static string StatusText(ProviderListingStatus status, DateOnly date)
    {
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return status switch
        {
            ProviderListingStatus.Verified => $"Verified {day}",
            ProviderListingStatus.Partial => $"Partly verified {day}",
            _ => $"Unverified {day}",
        };
    }

    /// <summary>Returns the line that says what the vendor states about residency, and that OmpOS did not verify it.</summary>
    /// <param name="note">The vendor's statement.</param>
    /// <returns>The line.</returns>
    public static string ResidencyText(string note) => $"Residency, as the vendor states it (not verified by OmpOS): {note}";

    /// <summary>Returns the line that names the page a listing was read on.</summary>
    /// <param name="url">The page.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public static string SourceLine(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return $"Source: {url.AbsoluteUri}";
    }

    /// <summary>Returns the line that names the vendor documentation.</summary>
    /// <param name="url">The page.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public static string DocsLine(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return $"{DocsActionLabel}: {url.AbsoluteUri}";
    }

    /// <summary>Returns the line that names the page where a key is created.</summary>
    /// <param name="url">The page.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public static string KeyPageLine(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return $"{KeyPageActionLabel}: {url.AbsoluteUri}";
    }

    /// <summary>Returns the line that names the vendor's rule for a coding plan.</summary>
    /// <param name="rule">The rule text.</param>
    /// <returns>The line.</returns>
    public static string PlanRuleText(string rule) => $"Vendor rule: {rule}";

    /// <summary>Returns the line of one plan tier.</summary>
    /// <param name="tier">The tier.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tier"/> is null.</exception>
    public static string TierLine(ProviderListingTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        return string.IsNullOrEmpty(tier.QuotaText) ? $"{tier.Name}: {tier.PriceText}" : $"{tier.Name}: {tier.PriceText}; {tier.QuotaText}";
    }

    /// <summary>Returns the sentence for a listing whose credential is an omp sign-in.</summary>
    /// <param name="command">The omp command.</param>
    /// <returns>The sentence.</returns>
    public static string SignInText(string command) =>
        $"Sign in inside omp with {command}. OmpOS never stores this credential and shows no key field for it.";

    /// <summary>Returns the accessible name of the set up action.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The name.</returns>
    public static string SetupActionName(string providerName) => $"Set up {providerName}";

    /// <summary>Returns the accessible name of the add action.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The name.</returns>
    public static string AddActionName(string providerName) => $"Add {providerName} to omp";

    /// <summary>Returns the accessible name of the model id field.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The name.</returns>
    public static string ModelIdsFieldName(string providerName) => $"Model ids for {providerName}";

    /// <summary>Returns the accessible name of the route choice.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The name.</returns>
    public static string RouteFieldName(string providerName) => $"Protocol and endpoint for {providerName}";

    /// <summary>Returns the accessible name of a base URL placeholder field.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <param name="label">The label of the placeholder.</param>
    /// <returns>The name.</returns>
    public static string VariableFieldName(string providerName, string label) => $"{label} for {providerName}";

    /// <summary>Returns the problem text of an empty placeholder.</summary>
    /// <param name="label">The label of the placeholder.</param>
    /// <returns>The text.</returns>
    public static string VariableMissing(string label) => $"Enter {label}.";

    /// <summary>Returns the problem text of a placeholder value that cannot be part of an address.</summary>
    /// <param name="label">The label of the placeholder.</param>
    /// <returns>The text.</returns>
    public static string VariableInvalid(string label) =>
        $"{label} must be an identifier of letters, digits, dots and hyphens, not a placeholder.";

    /// <summary>Returns the text for a provider added to omp.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The text.</returns>
    public static string Added(string providerName) => $"{providerName} was added to omp.";

    /// <summary>Returns the text of a listing that is already set up.</summary>
    /// <param name="takesKey">True when the listing's route takes a key.</param>
    /// <param name="credentialPresent">True when omp also holds a credential for it.</param>
    /// <returns>The text.</returns>
    public static string ConfiguredText(bool takesKey, bool credentialPresent)
    {
        if (!takesKey)
        {
            return "Set up in omp.";
        }

        return credentialPresent ? "Set up: omp holds a credential." : "Set up in omp. Add its key to use it.";
    }

    /// <summary>Returns the live-region text for the number of listings shown.</summary>
    /// <param name="count">How many listings are shown.</param>
    /// <returns>The text.</returns>
    public static string MatchCount(int count) =>
        count == 1 ? "1 listing shown." : $"{count.ToString(CultureInfo.InvariantCulture)} listings shown.";
}
