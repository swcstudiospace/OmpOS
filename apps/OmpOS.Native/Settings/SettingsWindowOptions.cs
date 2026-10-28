using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Commands.Handlers;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Safety;
using OmpOS.Presentation.Settings;
using OmpOS.Workspace;
using OmpOS.Workspace.Sets;
using IUrlLauncher = OmpOS.Native.Views.IUrlLauncher;

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

    /// <summary>Gets the user home directory the Rules, Skills and Commands pages read user-scope conventions from, or null for the current user's profile.</summary>
    public string? UserHome { get; init; }

    /// <summary>Gets the lookup of the environment variables the Tab page reads, or null to read the process environment.</summary>
    public Func<string, string?>? GetVariable { get; init; }

    /// <summary>Gets the slash commands the Commands page lists, or null for the shipped catalogue.</summary>
    public SlashCommandRegistry? Commands { get; init; }

    /// <summary>Gets what puts saved Runtime values in force for this run, or null for nothing.</summary>
    public Action<RuntimeSettingsValues>? ApplyRuntime { get; init; }

    /// <summary>Gets what is told when a saved Runtime value could not be written to the settings file, or null for nothing.</summary>
    public EventHandler<RuntimeSettingsSaveFailedEventArgs>? RuntimeSaveFailed { get; init; }

    /// <summary>Gets the hooks service the Hooks page shows, or null for a window without a Hooks page.</summary>
    public IHooksService? Hooks { get; init; }

    /// <summary>Gets the MCP catalog the MCP page shows, or null for a window without an MCP page.</summary>
    public McpServerCatalog? Mcp { get; init; }

    /// <summary>Gets the source of the Run mode page, or null for a window without one.</summary>
    public IRunModeSource? RunModes { get; init; }

    /// <summary>Gets the display settings the Display page edits, or null for a window without a Display page.</summary>
    public DisplaySettings? Display { get; init; }

    /// <summary>Gets the store of recent workspace sets the Workspaces page lists, or null for a window without a Workspaces page.</summary>
    public WorkspaceSetStore? WorkspaceSets { get; init; }

    /// <summary>Gets the remote environments host the Remote page shows, or null for a window without a Remote page.</summary>
    public IRemoteEnvironmentsHost? RemoteEnvironments { get; init; }

    /// <summary>Gets the cloud agents host the Cloud page shows. Null is a head without an API base address: the page then shows its not-configured state and no agent control.</summary>
    public ICloudAgentsHost? Cloud { get; init; }

    /// <summary>Gets the store of setup approvals the Worktrees page reads the consent state from, or null to read none (the page then reports the state as not granted).</summary>
    public ISetupConsentStore? WorktreeConsent { get; init; }

    /// <summary>Gets what opens the About window from the General page, or null when the head has none; the page then offers no About button.</summary>
    public Action? OpenAbout { get; init; }

    /// <summary>Gets the launcher the MCP page opens its configuration files with, or null when the head cannot open files.</summary>
    public IUrlLauncher? UrlLauncher { get; init; }

    /// <summary>Gets what runs a page's continuation on the context that owns the models, or null to run it inline.</summary>
    public Action<Action>? Dispatch { get; init; }

    /// <summary>Gets what is told when the Cloud page asks to open an agent's session, or null for nothing.</summary>
    public Action<CloudAgentOpenEventArgs>? OpenCloudAgent { get; init; }

    /// <summary>
    /// Gets or sets the Cloud page's model. It is created on first use and kept for the life of the head, so a closed and reopened window does
    /// not leave subscriptions on the host behind; the head disposes it.
    /// </summary>
    internal CloudAgentsViewModel? CloudAgentsModel { get; set; }
}
