using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>What <see cref="SettingsWindowFactory"/> needs to build the pages of the Settings window: where the values live and the seams a test replaces.</summary>
internal sealed class SettingsWindowOptions
{
    /// <summary>Gets the settings file the Runtime, General and Agents pages read and write.</summary>
    public required FileSettingsStore Store { get; init; }

    /// <summary>Gets the workspace directory the Rules and Skills pages list.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Gets the plugins root the Plugins page inspects.</summary>
    public required string PluginsRoot { get; init; }

    /// <summary>
    /// Gets how the Models page is built, or null for a window without one. It is called once for each window that opens, so the page
    /// starts from what omp holds then.
    /// </summary>
    public Func<ISettingsPage>? ModelsPageFactory { get; init; }

    /// <summary>Gets the source of the Network page's diagnostics, or null for one over the provider catalogue alone, with no managed provider. A run happens only when the user asks.</summary>
    public INetworkDiagnosticsSource? NetworkSource { get; init; }

    /// <summary>Gets the lookup of the environment variables the Tab page reads, or null to read the process environment.</summary>
    public Func<string, string?>? GetVariable { get; init; }

    /// <summary>Gets the slash commands the Commands page lists, or null for the shipped catalogue.</summary>
    public SlashCommandRegistry? Commands { get; init; }

    /// <summary>Gets what puts saved Runtime values in force for this run, or null for nothing.</summary>
    public Action<RuntimeSettingsValues>? ApplyRuntime { get; init; }

    /// <summary>Gets what is told when a saved Runtime value could not be written to the settings file, or null for nothing.</summary>
    public EventHandler<RuntimeSettingsSaveFailedEventArgs>? RuntimeSaveFailed { get; init; }
}
