using System.Text.Json.Serialization;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>One category row.</summary>
public sealed class CatalogueCategory
{
    /// <summary>Category name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Default tier name from the JSON property <c>defaultTier</c>.</summary>
    [JsonPropertyName("defaultTier")]
    public string DefaultTierName { get; set; } = "Full";

    /// <summary>Parsed tier.</summary>
    [JsonIgnore]
    public ComputerGrantTier DefaultTier =>
        Enum.TryParse(DefaultTierName, ignoreCase: false, out ComputerGrantTier tier) ? tier : ComputerGrantTier.Full;

    /// <summary><see langword="true"/> when actions above view always ask.</summary>
    public bool Raised { get; set; }

    /// <summary>Identifiers.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IList<CatalogueIdentifier> Identifiers { get; } = new List<CatalogueIdentifier>();
}
