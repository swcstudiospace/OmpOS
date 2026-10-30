using System.Collections;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Native.Views.Hooks;
using Autonogrammer.Native.Views.Mcp;
using Autonogrammer.Presentation.Automations;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Hooks;
using Autonogrammer.Presentation.Mcp;
using Autonogrammer.Presentation.Remote;
using Autonogrammer.Presentation.Safety;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;
using Autonogrammer.Presentation.Settings.Ignore;
using Autonogrammer.Presentation.Settings.Privacy;
using Autonogrammer.Presentation.Settings.Workspaces;
using Autonogrammer.Presentation.Settings.Worktrees;

namespace Autonogrammer.Native.Settings;

/// <summary>Builds the pages of the Settings window in navigation order and the window model over them.</summary>
internal static class SettingsWindowFactory
{
    /// <summary>
    /// Creates the pages in navigation order: Runtime, General, Display, Agents, Run mode, Models, Tab, Rules, Skills, Commands, Hooks, MCP,
    /// Plugins, Workspaces, Worktrees, Ignore, Remote, Cloud, Automations, Privacy, Network and Keyboard. A page whose service the options do not supply
    /// (Display, Run mode, Models, Hooks, MCP, Workspaces, Remote, Automations) is left out; the Cloud page is always there and shows its not-configured state
    /// when the options hold no cloud host.
    /// </summary>
    /// <param name="options">Where the values live.</param>
    /// <returns>The pages, unloaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static IReadOnlyList<ISettingsPage> CreatePages(SettingsWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runtime = new RuntimeSettingsBinding(options.Store, options.ApplyRuntime ?? (static _ => { }));
        if (options.RuntimeSaveFailed is { } failed)
        {
            runtime.Failed += failed;
        }

        var tab = options.GetVariable is { } getVariable ? new TabSettingsSource(getVariable) : new TabSettingsSource();
        var userHome = options.UserHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pages = new List<ISettingsPage>
        {
            runtime.CreatePage(),
            new GeneralPage(new GeneralSettingsSource(options.Store), options.OpenAbout),
        };
        if (options.Display is { } display)
        {
            pages.Add(new DisplaySettingsPage(display));
        }

        pages.Add(new AgentsPage(new AgentsSettingsSource(options.Store)));
        if (options.RunModes is { } runModes)
        {
            pages.Add(new RunModeSettingsPage(SettingsSection.RunMode, runModes));
        }

        if (options.ModelsPageFactory is { } models)
        {
            pages.Add(models());
        }

        AddConventionPages(pages, options, tab, userHome);
        pages.Add(new PluginsPage(new PluginsSettingsSource(options.PluginsRoot)));
        if (options.WorkspaceSets is { } workspaceSets)
        {
            pages.Add(new WorkspacesSettingsPage(new WorkspacesSettingsSource(workspaceSets)));
        }

        pages.Add(new WorktreesSettingsPage(new WorktreesSettingsSource(options.WorkspaceRoot, options.WorktreeConsent ?? NeverGrantedConsentStore.Instance)));
        pages.Add(new IgnoreSettingsPage(new IgnoreSettingsSource(options.Store, options.WorkspaceRoot)));
        if (options.RemoteEnvironments is { } remote)
        {
            var runtimeModel = options.ContainerEnvironments is { } containers && options.ContainerLister is { } lister
                ? new RuntimeEnvironmentsViewModel(remote, containers, lister)
                : null;
            pages.Add(new RemoteEnvironmentsSettingsPage(new RemoteEnvironmentsViewModel(remote), runtimeModel));
        }

        var cloud = options.CloudAgentsModel ??= CreateCloudModel(options);
        pages.Add(new CloudSettingsPage(cloud));
        if (options.Automations is { } automations)
        {
            pages.Add(new AutomationsSettingsPage(new HostedAutomationsViewModel(automations)));
        }

        pages.Add(new PrivacySettingsPage());
        pages.Add(new NetworkSettingsPage(options.NetworkSource ?? new NetworkDiagnosticsSource()));
        pages.Add(new KeyboardSettingsPage(new KeyboardSettingsSource()));
        return pages;
    }

    // Tab, Rules, Skills, Commands, Hooks and MCP: the pages of what the user's files and the hooks service supply.
    private static void AddConventionPages(List<ISettingsPage> pages, SettingsWindowOptions options, TabSettingsSource tab, string userHome)
    {
        pages.Add(new TabPage(tab));
        pages.Add(new RulesSettingsPage(new RulesSettingsSource(options.WorkspaceRoot, userHome)));
        pages.Add(new SkillsSettingsPage(new SkillsSettingsSource(options.WorkspaceRoot, userHome)));
        pages.Add(new CommandsSettingsPage(new SlashCommandsSettingsSource(
            options.Commands ?? SlashCommandCatalogue.Shipped,
            new ConventionsCommandsSource(options.WorkspaceRoot, userHome))));
        if (options.Hooks is { } hooks)
        {
            var workspaceRoot = options.WorkspaceRoot;
            pages.Add(new HooksSettingsPage(
                SettingsSection.Hooks,
                new HooksServiceSource(hooks, () => new HooksWorkspace { Roots = [workspaceRoot] })));
        }

        if (options.Mcp is not null)
        {
            pages.Add(CreateMcpPage(options, userHome));
        }
    }

    /// <summary>Creates the window model over a new set of pages. The first page, Runtime, is selected.</summary>
    /// <param name="options">Where the values live.</param>
    /// <returns>The model, unloaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static SettingsWindowModel CreateModel(SettingsWindowOptions options) => new(CreatePages(options));

    private static McpSettingsPage CreateMcpPage(SettingsWindowOptions options, string userHome)
    {
        var catalog = options.Mcp!;
        var root = options.WorkspaceRoot;
        McpLocatorContext Context() => new([root], userHome, EnvironmentVariables());

        Func<string, CancellationToken, Task>? openFile = null;
        if (options.UrlLauncher is { } launcher)
        {
            openFile = async (path, cancellationToken) =>
            {
                var opened = await launcher.OpenAsync(new Uri(Path.GetFullPath(path)), cancellationToken).ConfigureAwait(true);
                if (!opened)
                {
                    throw new McpSourceException(HeadSettingsCopy.McpFileNotOpened);
                }
            };
        }

        return new McpSettingsPage(new McpSettingsViewModel(new McpCatalogSettingsSource(catalog, Context, openFile)));
    }

    private static CloudAgentsViewModel CreateCloudModel(SettingsWindowOptions options)
    {
        var model = new CloudAgentsViewModel(
            options.Cloud ?? new NotConfiguredCloudHost(),
            TimeProvider.System,
            new CloudAgentsOptions { Dispatch = options.Dispatch });
        model.OpenInAgentsRequested += (_, request) => options.OpenCloudAgent?.Invoke(request);
        return model;
    }

    private static Dictionary<string, string?> EnvironmentVariables()
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name)
            {
                variables[name] = entry.Value as string;
            }
        }

        return variables;
    }
}
