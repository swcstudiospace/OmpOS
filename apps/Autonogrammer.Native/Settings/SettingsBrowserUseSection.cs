using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Native.Settings;

/// <summary>The <c>browserUse</c> object of <c>settings.json</c>. Members this build does not know are kept and written back unchanged.</summary>
internal sealed class SettingsBrowserUseSection
{
    /// <summary>Gets or sets whether the agent browser is enabled. Only the JSON literal <c>true</c> turns it on; null means off.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Gets or sets an explicit Chromium path. Empty or null means locate.</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Gets or sets the origin policy name.</summary>
    public string? PolicyMode { get; set; }

    /// <summary>Gets or sets hosts allowed in strict mode.</summary>
    public List<string>? AllowedHosts { get; set; }

    /// <summary>Gets or sets hosts that are always denied.</summary>
    public List<string>? DeniedHosts { get; set; }

    /// <summary>Gets or sets the download policy name.</summary>
    public string? DownloadMode { get; set; }

    /// <summary>Gets or sets the sandbox policy name.</summary>
    public string? SandboxMode { get; set; }

    /// <summary>Gets or sets whether a named profile is kept. Null means false.</summary>
    public bool? PersistentProfile { get; set; }

    /// <summary>Gets or sets whether dispatch-origin turns may use the browser. Null means false.</summary>
    public bool? AllowDispatchSessions { get; set; }

    /// <summary>Gets or sets whether granted local origins are treated as trusted. Null means false.</summary>
    public bool? LocalOriginsTrusted { get; set; }

    /// <summary>Gets or sets the members of a newer schema that this build does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
