namespace Autonogrammer.Presentation.Settings;

/// <summary>The read-only list the Plugins page shows.</summary>
/// <param name="Rows">The plugins in composition order.</param>
public sealed record PluginsSettingsValues(IReadOnlyList<PluginRow> Rows);
