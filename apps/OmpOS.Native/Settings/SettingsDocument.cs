using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpOS.Native.Settings;

/// <summary>
/// The JSON shape of <c>settings.json</c>. Every member is optional; an absent member keeps its default. The document holds no key
/// material of any kind: provider credentials belong to omp. Members this build does not know are kept in <see cref="Extra"/>, so a
/// rewrite by this build never drops what a newer build of the same major schema wrote.
/// </summary>
internal sealed class SettingsDocument
{
    /// <summary>Gets or sets the major version of the schema. Absent means the first version.</summary>
    public int? SchemaVersion { get; set; }

    /// <summary>Gets or sets the motion setting: <c>system</c>, <c>reduced</c> or <c>full</c>.</summary>
    public string? Motion { get; set; }

    /// <summary>Gets or sets the theme setting: <c>system</c>, <c>light</c> or <c>dark</c>. Absent means light.</summary>
    public string? Theme { get; set; }

    /// <summary>Gets or sets the terminal settings.</summary>
    public SettingsTerminalSection? Terminal { get; set; }

    /// <summary>Gets or sets the runtime settings.</summary>
    public SettingsRuntimeSection? Runtime { get; set; }

    /// <summary>Gets or sets the ignore settings: the global patterns and the two switches.</summary>
    public SettingsIgnoreSection? Ignore { get; set; }

    /// <summary>Gets or sets the members of a newer schema that this build does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
