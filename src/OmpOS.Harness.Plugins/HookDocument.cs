using System.Text.Json.Serialization;

namespace OmpOS.Harness.Plugins;

/// <summary>
/// The wire shape of one hook.
/// </summary>
internal sealed class HookDocument
{
    [JsonRequired]
    public string? Trigger { get; set; }

    [JsonRequired]
    public List<string?>? Command { get; set; }

    [JsonRequired]
    public string? WorkingDirectory { get; set; }

    public bool Blocking { get; set; }
}
