using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Parsed catalogue.</summary>
public sealed class CatalogueFile
{
    /// <summary>Category rows.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IList<CatalogueCategory> Categories { get; } = new List<CatalogueCategory>();

    /// <summary>Denylist groups.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IList<CatalogueDenylistGroup> DenylistGroups { get; } = new List<CatalogueDenylistGroup>();

    /// <summary>Every built-in denylist identifier.</summary>
    [JsonIgnore]
    public IReadOnlyList<CatalogueIdentifier> Denylist => DenylistGroups.SelectMany(static group => group.Identifiers).ToArray();
}
