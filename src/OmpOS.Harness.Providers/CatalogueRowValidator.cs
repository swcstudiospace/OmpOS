using System.Text.RegularExpressions;

namespace OmpOS.Harness.Providers;

/// <summary>
/// Checks the invariants every catalogue row must hold: a slug id unique in the catalogue, an https source, a status with a date, a
/// route that is https (or loopback http for a local runtime) without a placeholder host, URL variables that are all declared and all
/// used, a rule text on every coding plan, and a "verify before use" note on every
/// unverified row. The check is pure: no network, no file access.
/// </summary>
public static partial class CatalogueRowValidator
{
    /// <summary>The longest note a row may carry.</summary>
    public const int MaxNoteLength = 240;

    /// <summary>The prefix an unverified row's note must start with.</summary>
    public const string VerifyPrefix = "Verify before use:";

    private static readonly string[] PlaceholderMarkers = ["example.com", "example.org", "your-host", "your-resource", "yourhost", "<", ">"];

    /// <summary>Returns every problem of <paramref name="rows"/>; an empty list means the rows are valid.</summary>
    /// <param name="rows">The rows to check.</param>
    /// <returns>One sentence per problem, each starting with the row id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public static IReadOnlyList<string> Validate(IReadOnlyList<CatalogueRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var problems = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var ompIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!ids.Add(row.Id))
            {
                problems.Add($"{row.Id}: the id is used by two rows");
            }

            CheckRow(row, ompIds, problems);
        }

        return problems;
    }

    private static void CheckRow(CatalogueRow row, HashSet<string> ompIds, List<string> problems)
    {
        void Bad(string message) => problems.Add($"{row.Id}: {message}");

        if (!SlugPattern().IsMatch(row.Id))
        {
            Bad("the id is not a slug");
        }

        if (string.IsNullOrWhiteSpace(row.DisplayName))
        {
            Bad("the display name is empty");
        }

        if (!Enum.IsDefined(row.Region) || !Enum.IsDefined(row.Kind) || !Enum.IsDefined(row.Status))
        {
            Bad("region, kind or status is not a defined value");
        }

        if (row.Country is not null && !CountryPattern().IsMatch(row.Country))
        {
            Bad("the country is not an ISO 3166-1 alpha-2 code");
        }

        if ((row.Kind == CatalogueKind.Local) != (row.Region == CatalogueRegion.Local))
        {
            Bad("the local kind and the local region go together");
        }

        if (row.OmpProviderId is not null && !ompIds.Add(row.OmpProviderId))
        {
            Bad($"the omp provider id {row.OmpProviderId} is used by two rows");
        }

        if (row.ApiKeyEnv is not null && !EnvPattern().IsMatch(row.ApiKeyEnv))
        {
            Bad("the key setting must be an environment variable NAME");
        }

        if (!IsHttps(row.SourceUrl) || row.StatusDate == default)
        {
            Bad("every row needs an https source URL and a status date");
        }

        if ((row.DocsUrl is not null && !IsHttps(row.DocsUrl)) || (row.KeyPageUrl is not null && !IsHttps(row.KeyPageUrl)))
        {
            Bad("the docs and key page URLs must be https");
        }

        if (row.Status == CatalogueStatus.Unverified && row.Note?.StartsWith(VerifyPrefix, StringComparison.Ordinal) != true)
        {
            Bad($"an unverified row needs a note that starts with \"{VerifyPrefix}\"");
        }

        if (row.Note is { Length: > MaxNoteLength })
        {
            Bad($"the note is longer than {MaxNoteLength} characters");
        }

        CheckRoutes(row, Bad);
        CheckPlan(row, Bad);
    }

    private static void CheckRoutes(CatalogueRow row, Action<string> bad)
    {
        if (row.Routes.Count == 0 && row.LoginCommand is null)
        {
            bad("a row needs a route or an omp sign-in command");
        }

        var declared = row.UrlVariables.Select(variable => variable.Name).ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var routeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in row.Routes)
        {
            if (!SlugPattern().IsMatch(route.Id) || !routeIds.Add(route.Id))
            {
                bad($"route id '{route.Id}' is not a slug or is repeated");
            }

            if (!CatalogueApiNames.IsKnown(route.Api))
            {
                bad($"route {route.Id}: '{route.Api}' is not an omp api value");
            }

            if ((route.Auth == CatalogueAuthStyle.CustomHeader) != !string.IsNullOrEmpty(route.AuthHeader))
            {
                bad($"route {route.Id}: the auth header is set exactly for custom header auth");
            }

            foreach (var name in CatalogueUrlTemplate.VariablesIn(route.BaseAddressTemplate))
            {
                used.Add(name);
            }

            CheckBaseUrl(row, route, bad);
        }

        if (!declared.SetEquals(used))
        {
            bad("the URL variables the base URLs use and the ones the row declares differ");
        }
    }

    private static void CheckBaseUrl(CatalogueRow row, CatalogueRoute route, Action<string> bad)
    {
        var filled = Placeholder().Replace(route.BaseAddressTemplate, "x");
        if (filled.Contains('{', StringComparison.Ordinal) || filled.Contains('}', StringComparison.Ordinal))
        {
            bad($"route {route.Id}: the base URL has a malformed placeholder");
            return;
        }

        if (!Uri.TryCreate(filled, UriKind.Absolute, out var url) || url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0)
        {
            bad($"route {route.Id}: the base URL is not an absolute address without user information, query or fragment");
            return;
        }

        if (PlaceholderMarkers.Any(marker => filled.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            bad($"route {route.Id}: the base URL has a placeholder host");
        }

        if (string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            if (row.Region != CatalogueRegion.Local || !url.IsLoopback)
            {
                bad($"route {route.Id}: plain http is only allowed for a local runtime on a loopback host");
            }
        }
        else if (!string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            bad($"route {route.Id}: the base URL is not http or https");
        }
    }

    private static void CheckPlan(CatalogueRow row, Action<string> bad)
    {
        if (row.Kind != CatalogueKind.CodingPlan)
        {
            if (row.Plan is not null)
            {
                bad("only a coding plan row carries plan terms");
            }

            return;
        }

        var plan = row.Plan;
        if (plan is null)
        {
            bad("a coding plan row needs plan terms");
            return;
        }

        if (string.IsNullOrWhiteSpace(plan.RuleText))
        {
            bad("the plan has no rule text");
        }

        if (!plan.CloudDefaultPayAsYouGo)
        {
            bad("cloud agents must default to pay-as-you-go keys");
        }

        if (!IsHttps(plan.PlanUrl) || (plan.RuleSourceUrl is not null && !IsHttps(plan.RuleSourceUrl)))
        {
            bad("the plan and rule source URLs must be https");
        }

        if (plan.Tiers.Count == 0)
        {
            bad("the plan has no tier");
        }

        foreach (var tier in plan.Tiers)
        {
            if (string.IsNullOrWhiteSpace(tier.Name) || string.IsNullOrWhiteSpace(tier.PriceText))
            {
                bad("a tier needs a name and a price text");
            }

            if (!tier.PriceVerified && !string.Equals(tier.PriceText, CataloguePlanTier.UnverifiedPrice, StringComparison.Ordinal))
            {
                bad($"tier {tier.Name}: a price that is not verified must read \"{CataloguePlanTier.UnverifiedPrice}\"");
            }
        }
    }

    private static bool IsHttps(Uri url) => url.IsAbsoluteUri && string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SlugPattern();

    [GeneratedRegex(@"^[A-Z]{2}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CountryPattern();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnvPattern();

    [GeneratedRegex(@"\{[a-z][a-z0-9_]*\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();
}
