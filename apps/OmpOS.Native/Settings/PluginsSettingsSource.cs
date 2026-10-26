using System.Text.Json;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>
/// The Plugins page's rows: the four plugins of <see cref="OmpPluginCatalog"/> in composition order, each resolved against the plugins
/// root. A plugin is Found when it resolves to <see cref="PluginHealth.Ready"/> and Missing otherwise. The version is the one the plugin's
/// own <c>package.json</c> declares, or null when it has none. Nothing is run; there is no switch (OQ-80).
/// </summary>
internal sealed class PluginsSettingsSource : IPluginsSettingsSource
{
    private const int MaxManifestBytes = 1024 * 1024;
    private const int MaxJsonDepth = 16;
    private const string ManifestName = "package.json";

    private readonly string _pluginsRoot;

    /// <summary>Initializes a new instance of the <see cref="PluginsSettingsSource"/> class.</summary>
    /// <param name="pluginsRoot">The plugins root. It may not exist: every plugin is then Missing.</param>
    /// <exception cref="ArgumentException"><paramref name="pluginsRoot"/> is null, empty or white space.</exception>
    public PluginsSettingsSource(string pluginsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRoot);
        _pluginsRoot = pluginsRoot;
    }

    /// <inheritdoc/>
    public Task<PluginsSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        Task.Run(() => new PluginsSettingsValues([.. OmpPluginCatalog.BuiltIn.Select(ToRow)]), cancellationToken);

    private static string? ReadVersion(string directory)
    {
        try
        {
            var path = Path.Combine(directory, ManifestName);
            if (!File.Exists(path) || new FileInfo(path).Length > MaxManifestBytes)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("version", out var version)
                && version.ValueKind == JsonValueKind.String
                && version.GetString() is { Length: > 0 } text
                    ? text
                    : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private PluginRow ToRow(OmpPluginDescriptor descriptor)
    {
        var resolved = OmpPluginResolver.Resolve(_pluginsRoot, descriptor);
        var found = resolved.Health == PluginHealth.Ready;
        return new PluginRow(
            descriptor.Id,
            descriptor.DisplayName,
            HeadSettingsCopy.PluginRole(descriptor.Role),
            ReadVersion(resolved.AbsoluteDirectory),
            found ? PluginRow.FoundAvailability : PluginRow.MissingAvailability,
            found ? HeadSettingsCopy.PluginFoundDetail(descriptor) : HeadSettingsCopy.PluginMissingDetail(descriptor));
    }
}
