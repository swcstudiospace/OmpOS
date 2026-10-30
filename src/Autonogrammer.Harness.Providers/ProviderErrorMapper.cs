using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.Providers;

/// <summary>
/// Turns a failed provider request into an actionable message. The mapping uses only what the research records. HTTP 429
/// is split into "quota used up" and "too many requests" by the provider's own error text, using the two texts the iFlytek
/// page publishes (providers-eu-cn.md:30 and its primary page, section 8) and the common English wording; a 429 without text
/// is read as quota for a coding plan, whose quota is the usual limit, and as rate limiting elsewhere. HTTP 402 is read as
/// an exhausted balance. A 401 or 403 is a plan expiry only when the caller supplies a plan end date that has passed, because
/// the research records no provider error code that names expiry. Vendor error codes beyond those are not mapped (UNKNOWN:
/// run each provider's failing request and record the body before adding a code).
/// </summary>
public sealed class ProviderErrorMapper
{
    private static readonly string[] QuotaWords = ["quota", "balance", "billing", "配额", "额度"];
    private static readonly string[] RateWords = ["rate limit", "rate-limit", "too many", "速率"];

    private readonly TimeProvider _time;

    /// <summary>Creates a mapper that reads today's date from <paramref name="time"/>.</summary>
    /// <param name="time">The clock used to decide whether a plan end date has passed.</param>
    public ProviderErrorMapper(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Maps a failed request to a message for the user.</summary>
    /// <param name="entry">The provider entry the request went to.</param>
    /// <param name="failure">What the caller observed.</param>
    public ProviderErrorMessage Map(ProviderEntry entry, ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(failure);

        var plan = ProviderCatalogue.FindCodingPlan(entry.Id);
        var name = plan?.DisplayName ?? entry.Id;
        var url = plan?.PlanUrl;
        var status = failure.HttpStatus.ToString(CultureInfo.InvariantCulture);

        return failure.HttpStatus switch
        {
            401 or 403 => MapRejected(plan, name, status, failure.PlanValidUntil, url),
            402 => new ProviderErrorMessage(ProviderErrorKind.QuotaExhausted, QuotaMessage(plan, name, status), url),
            429 => MapTooMany(plan, name, status, failure.Detail, url),
            >= 500 and <= 599 => new ProviderErrorMessage(
                ProviderErrorKind.ServiceUnavailable,
                $"{name} failed on its side (HTTP {status}). Your key and quota are not the cause. Wait a moment and retry, or switch to another provider.",
                url),
            _ => new ProviderErrorMessage(
                ProviderErrorKind.Unrecognised,
                $"{name} answered HTTP {status}, which Autonogrammer does not map. Check the endpoint and the provider's status page.",
                url),
        };
    }

    private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Window(CodingPlanQuotaWindow window) => window switch
    {
        CodingPlanQuotaWindow.FiveHours => "5 hours",
        CodingPlanQuotaWindow.Week => "week",
        _ => "subscription month",
    };

    private static string Limits(CodingPlanEntry plan)
    {
        var tiers = new List<string>();
        foreach (var tier in plan.Tiers.Where(candidate => candidate.Quotas.Count > 0))
        {
            var builder = new StringBuilder(tier.Name).Append(" about ");
            builder.AppendJoin(", ", tier.Quotas.Select(quota => $"{Count(quota.Requests)} requests per {Window(quota.Window)}"));
            tiers.Add(builder.ToString());
        }

        return tiers.Count == 0
            ? $"{plan.DisplayName} publishes no request limits, so check your usage on the plan page."
            : "Published limits: " + string.Join("; ", tiers) + ".";
    }

    private static string QuotaMessage(CodingPlanEntry? plan, string name, string status)
    {
        if (plan is null)
        {
            return $"{name} refused the request because the account has no quota or balance left (HTTP {status}). Top up the balance or raise the spending limit with the provider, or switch to another provider.";
        }

        return $"{name} refused the request because the plan's request quota is used up (HTTP {status}). {Limits(plan)} "
            + $"Wait for the window to reset, upgrade or add quota at {plan.PlanUrl}, or switch to another provider meanwhile.";
    }

    private static bool ContainsAny(string? text, string[] words) =>
        text is not null && words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static ProviderErrorMessage MapTooMany(CodingPlanEntry? plan, string name, string status, string? detail, Uri? url)
    {
        var quota = ContainsAny(detail, QuotaWords);
        var rate = ContainsAny(detail, RateWords);
        if (quota || (!rate && plan is not null))
        {
            return new ProviderErrorMessage(ProviderErrorKind.QuotaExhausted, QuotaMessage(plan, name, status), url);
        }

        return new ProviderErrorMessage(
            ProviderErrorKind.RateLimited,
            $"{name} is limiting how fast requests arrive (HTTP {status}). Wait a moment and retry, and avoid running several sessions against one key at once.",
            url);
    }

    private ProviderErrorMessage MapRejected(CodingPlanEntry? plan, string name, string status, DateOnly? validUntil, Uri? url)
    {
        if (plan is not null && validUntil is { } last && DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime) > last)
        {
            var validity = plan.ValidityDays is { } days
                ? string.Create(CultureInfo.InvariantCulture, $" Each paid period is valid for {days} days.")
                : string.Empty;
            return new ProviderErrorMessage(
                ProviderErrorKind.PlanExpired,
                $"{name} rejected the key (HTTP {status}) because the plan ended on {last.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.{validity} "
                    + $"Renew the plan at {plan.PlanUrl}, then enter the new plan key.",
                url);
        }

        if (plan is null)
        {
            return new ProviderErrorMessage(
                ProviderErrorKind.CredentialRejected,
                $"{name} refused the credential (HTTP {status}). Check the key, the header it is sent in and the base address, then try again.",
                url);
        }

        var note = plan.ValidityDays is { } period
            ? string.Create(CultureInfo.InvariantCulture, $" A key also stops working when the {period}-day paid period ends.")
            : string.Empty;
        var separate = plan.SeparateKey ? " Use the plan's own key, not the provider's pay-per-use key." : string.Empty;
        return new ProviderErrorMessage(
            ProviderErrorKind.CredentialRejected,
            $"{name} refused the key (HTTP {status}).{separate}{note} Check the key and the base address, or renew the plan at {plan.PlanUrl}.",
            url);
    }
}
