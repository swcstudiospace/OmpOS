using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>The order of the Settings pages in the web head: the desktop order, which follows the Cursor settings sidebar.</summary>
internal static class WebSettingsOrder
{
    /// <summary>Gets the sections in navigation order. A section not listed sorts after the listed ones, by its number.</summary>
    internal static IReadOnlyList<SettingsSection> Sections { get; } =
    [
        SettingsSection.General,
        SettingsSection.Runtime,
        SettingsSection.Agents,
        SettingsSection.RunMode,
        SettingsSection.Models,
        SettingsSection.Tab,
        SettingsSection.Rules,
        SettingsSection.Skills,
        SettingsSection.Commands,
        SettingsSection.Hooks,
        SettingsSection.Mcp,
        SettingsSection.Plugins,
        SettingsSection.Workspaces,
        SettingsSection.Remote,
        SettingsSection.Cloud,
        SettingsSection.Privacy,
        SettingsSection.Network,
        SettingsSection.Keyboard,
    ];

    /// <summary>Returns the sort key of a section.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The index in <see cref="Sections"/>, or a number after every listed section.</returns>
    internal static int KeyOf(SettingsSection section)
    {
        for (var index = 0; index < Sections.Count; index++)
        {
            if (Sections[index] == section)
            {
                return index;
            }
        }

        return Sections.Count + (int)section;
    }
}
