using System.Text.Json.Serialization;
namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>One denylist group.</summary>
public sealed class CatalogueDenylistGroup
{
    /// <summary>Group kind, such as terminal or own-process.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Identifiers in the group.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IList<CatalogueIdentifier> Identifiers { get; } = new List<CatalogueIdentifier>();
}
