using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Tests.Settings;

/// <summary>A scripted source of the Plugins page. It has no save.</summary>
internal sealed class FakePluginsSource : FakeSettingsSource<PluginsSettingsValues>, IPluginsSettingsSource
{
    /// <summary>Creates a source that lists the four plugins, one of them missing.</summary>
    public FakePluginsSource()
        : base(new PluginsSettingsValues(
        [
            new PluginRow("ultrathink", "Ultrathink", "Rewrites a prompt into a spec", "1.2.0", PluginRow.FoundAvailability, "/plugins/ultrathink"),
            new PluginRow("swarm", "Swarm", "Runs subagents", null, PluginRow.FoundAvailability, null),
            new PluginRow("substrate", "Substrate", "Steers sessions", "0.9.1", PluginRow.FoundAvailability, null),
            new PluginRow("gsd", "GSD-Core", "Plans and executes phases", "3.0.0", PluginRow.MissingAvailability, "Not installed"),
        ]))
    {
    }

    /// <inheritdoc />
    public Task<PluginsSettingsValues> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);
}
