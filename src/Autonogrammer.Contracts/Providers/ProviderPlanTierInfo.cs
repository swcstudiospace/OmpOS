using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>One tier of a coding plan.</summary>
public sealed record ProviderPlanTierInfo
{
    /// <summary>Gets the tier name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the price as text.</summary>
    public required string PriceText { get; init; }

    /// <summary>Gets a value indicating whether the research verified the price.</summary>
    public required bool PriceVerified { get; init; }

    /// <summary>Gets the published usage limits as text, or null when the vendor publishes none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QuotaText { get; init; }
}
