using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Native.Settings;

/// <summary>The <c>runtime</c> object of <c>settings.json</c>. Members this build does not know are kept and written back unchanged.</summary>
internal sealed class SettingsRuntimeSection
{
    /// <summary>Gets or sets the whole hours of inactivity after which a session sleeps, or null for the default.</summary>
    public int? IdleStopHours { get; set; }

    /// <summary>Gets or sets the whole days a completed session is kept, or null for the default.</summary>
    public int? RetentionDays { get; set; }

    /// <summary>Gets or sets the members of a newer schema that this build does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
