namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Why a plugin manifest was rejected. No code from a rejected manifest runs.
/// </summary>
public enum PluginRejectionKind
{
    /// <summary>The manifest is larger than the limit. It was rejected before parsing.</summary>
    ManifestTooLarge,

    /// <summary>The manifest is not valid JSON, is not an object, has a missing or mistyped field, or is not a single object.</summary>
    ManifestMalformed,

    /// <summary>The manifest contains a field that the schema does not define.</summary>
    UnknownField,

    /// <summary>The manifest repeats a field name.</summary>
    DuplicateField,

    /// <summary>A field has a value that the schema's rules reject.</summary>
    InvalidManifest,

    /// <summary>The manifest's API version is outside the range this host supports.</summary>
    UnsupportedApiVersion,

    /// <summary>Another manifest with the same plugin identifier is already loaded.</summary>
    DuplicatePluginId,
}
