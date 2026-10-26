using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpOS.Native.Settings;

/// <summary>The <c>terminal</c> object of <c>settings.json</c>. Members this build does not know are kept and written back unchanged.</summary>
internal sealed class SettingsTerminalSection
{
    /// <summary>Gets or sets the explicit auto-run opt-in. Only the JSON literal <c>true</c> turns auto-run on; null means off.</summary>
    public bool? AutoRun { get; set; }

    /// <summary>Gets or sets the members of a newer schema that this build does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
