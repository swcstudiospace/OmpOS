namespace Autonogrammer.Harness.Providers;

/// <summary>The persisted provider registry: a format version and one entry per custom provider. No key is stored.</summary>
internal sealed class ProviderRegistryDocument
{
    /// <summary>The only format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the format version.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the custom providers.</summary>
    public List<CustomProviderDocument>? Providers { get; set; }
}
