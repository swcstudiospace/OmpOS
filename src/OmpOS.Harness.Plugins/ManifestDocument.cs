using System.Text.Json.Serialization;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// The wire shape of a manifest. Fields are nullable so that a JSON null is reported by the value rules, and
/// <see cref="JsonRequiredAttribute"/> reports a missing field.
/// </summary>
internal sealed class ManifestDocument
{
    [JsonRequired]
    public string? Id { get; set; }

    [JsonRequired]
    public string? Version { get; set; }

    [JsonRequired]
    public int? ApiVersion { get; set; }

    [JsonRequired]
    public List<string?>? Capabilities { get; set; }

    [JsonRequired]
    public List<string?>? Permissions { get; set; }

    [JsonRequired]
    public List<HookDocument?>? Hooks { get; set; }
}
