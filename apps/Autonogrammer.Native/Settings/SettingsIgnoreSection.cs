using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Native.Settings;

/// <summary>The <c>ignore</c> object of <c>settings.json</c>. Members this build does not know are kept and written back unchanged.</summary>
internal sealed class SettingsIgnoreSection
{
    /// <summary>Gets or sets the patterns of the global ignore list in gitignore syntax, or null for none.</summary>
    public List<string>? GlobalPatterns { get; set; }

    /// <summary>Gets or sets whether the built-in ignore list applies, or null for the default (it applies).</summary>
    public bool? UseDefaultList { get; set; }

    /// <summary>Gets or sets whether ignore files below the workspace root are read, or null for the default (they are not).</summary>
    public bool? Hierarchical { get; set; }

    /// <summary>Gets or sets the members of a newer schema that this build does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
