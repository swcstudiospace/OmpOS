using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Providers;

/// <summary>The terms of a coding plan provider.</summary>
public sealed record ProviderPlanInfo
{
    /// <summary>Gets the vendor's rule for using the plan key.</summary>
    public required string RuleText { get; init; }

    /// <summary>Gets the page that states the rule, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? RuleSourceUrl { get; init; }

    /// <summary>Gets a value indicating whether the vendor limits the key to interactive use in named tools.</summary>
    public required bool InteractiveOnly { get; init; }

    /// <summary>Gets a value indicating whether the plan has a key of its own.</summary>
    public required bool SeparateKey { get; init; }

    /// <summary>Gets the page of the plan.</summary>
    public required Uri PlanUrl { get; init; }

    /// <summary>Gets the tiers.</summary>
    public required IReadOnlyList<ProviderPlanTierInfo> Tiers { get; init; }

    /// <summary>Gets the tools the vendor lists for the plan.</summary>
    public required IReadOnlyList<string> ListedTools { get; init; }
}
