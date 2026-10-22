namespace OmpOS.Harness.Plugins;

/// <summary>
/// The persisted plugin state: quarantines with their reasons, consent hashes, and hook order and enablement.
/// </summary>
internal sealed class PluginStateDocument
{
    public List<QuarantineDocument> Quarantines { get; set; } = [];

    public List<string> ConsentedHashes { get; set; } = [];

    public List<string> HookOrder { get; set; } = [];

    public List<string> DisabledHooks { get; set; } = [];
}
