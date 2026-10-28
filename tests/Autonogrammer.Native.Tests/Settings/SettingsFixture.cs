using Avalonia.Controls;
using Avalonia.Threading;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Tests.Mcp;
using Autonogrammer.Native.Tests.Remote;
using Autonogrammer.Native.Tests.Safety;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Automations;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Commands.Handlers;
using Autonogrammer.Presentation.Hooks;
using Autonogrammer.Presentation.Mcp;
using Autonogrammer.Presentation.Remote;
using Autonogrammer.Presentation.Safety;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;
using Autonogrammer.Presentation.Settings.Ignore;
using Autonogrammer.Presentation.Settings.Models;
using Autonogrammer.Presentation.Settings.Privacy;
using Autonogrammer.Presentation.Settings.Workspaces;
using Autonogrammer.Presentation.Settings.Worktrees;
using Autonogrammer.Presentation.Tests.Hooks;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>
/// A real <see cref="SettingsWindow"/> over real page models whose sources are scripted. Only the sources are fake: the twenty-two pages, the
/// window model and the window are the product's. The pages are in the order the head's factory gives them when every service is supplied.
/// The window is shown, and every page is loaded unless the test asks to keep them loading.
/// </summary>
internal sealed class SettingsFixture : IDisposable
{
    private SettingsFixture()
    {
        Runtime = new RuntimePage(new RuntimeSettingsModel());
        Models = new ModelsSettingsPage(ModelsSource);
        General = new GeneralPage(GeneralSource, openAbout: () => AboutOpened++);
        Display = new DisplaySettingsPage(new DisplaySettings(DisplayStore));
        Agents = new AgentsPage(AgentsSource);
        RunMode = new RunModeSettingsPage(SettingsSection.RunMode, RunModeSource);
        Tab = new TabPage(TabSource);
        Plugins = new PluginsPage(PluginsSource);
        Rules = new RulesSettingsPage(RulesSource);
        Skills = new SkillsSettingsPage(SkillsSource);
        Commands = new CommandsSettingsPage(CommandsSource);
        Hooks = new HooksSettingsPage(SettingsSection.Hooks, HooksSource);
        Mcp = new McpSettingsPage(new McpSettingsViewModel(McpSource));
        Workspaces = new WorkspacesSettingsPage(WorkspacesSource);
        Worktrees = new WorktreesSettingsPage(WorktreesSource);
        Ignore = new IgnoreSettingsPage(IgnoreSource);
        Remote = new RemoteEnvironmentsSettingsPage(new RemoteEnvironmentsViewModel(RemoteHost), new RuntimeEnvironmentsViewModel(RemoteHost, ContainerHost, ContainerLister));
        CloudHost = new FakeCloudAgentsHost(Clock);
        CloudModel = new CloudAgentsViewModel(CloudHost, Clock);
        Cloud = new CloudSettingsPage(CloudModel);
        Automations = new AutomationsSettingsPage(new HostedAutomationsViewModel(AutomationsManager));
        Privacy = new PrivacySettingsPage();
        Network = new NetworkSettingsPage(NetworkSource);
        Keyboard = new KeyboardSettingsPage(KeyboardSource);
        Model = new SettingsWindowModel(
        [
            Runtime, General, Display, Agents, RunMode, Models, Tab, Rules, Skills, Commands, Hooks, Mcp, Plugins,
            Workspaces, Worktrees, Ignore, Remote, Cloud, Automations, Privacy, Network, Keyboard,
        ]);
        Window = new SettingsWindow(Model);
        Runtime.Saved += (_, args) => RuntimeSaved.Add(args.Values);
    }

    /// <summary>Gets the diagnostics source of the Network page.</summary>
    internal FakeNetworkSource NetworkSource { get; } = new();

    /// <summary>Gets the source of the Keyboard page.</summary>
    internal FakeKeyboardSource KeyboardSource { get; } = new();

    /// <summary>Gets the source of the General page.</summary>
    internal FakeGeneralSource GeneralSource { get; } = new();

    /// <summary>Gets the source of the Agents page.</summary>
    internal FakeAgentsSource AgentsSource { get; } = new();

    /// <summary>Gets the source of the Tab page.</summary>
    internal FakeTabSource TabSource { get; } = new();

    /// <summary>Gets the source of the Plugins page.</summary>
    internal FakePluginsSource PluginsSource { get; } = new();

    /// <summary>Gets the source of the Rules page.</summary>
    internal FakeCatalogSource RulesSource { get; } = new();

    /// <summary>Gets the source of the Skills page.</summary>
    internal FakeCatalogSource SkillsSource { get; } = new();

    /// <summary>Gets the source of the Commands page.</summary>
    internal FakeCatalogSource CommandsSource { get; } = new();

    /// <summary>Gets the source of the Models page.</summary>
    internal FakeModelsSource ModelsSource { get; } = new();

    /// <summary>Gets the store behind the Display page.</summary>
    internal MemoryDisplayStore DisplayStore { get; } = new();

    /// <summary>Gets the source of the Run mode page.</summary>
    internal FakeRunModeSource RunModeSource { get; } = new();

    /// <summary>Gets the source of the Hooks page.</summary>
    internal ScriptedHooksPageSource HooksSource { get; } = new();

    /// <summary>Gets the source of the MCP page.</summary>
    internal ScriptedMcpPageSource McpSource { get; } = new();

    /// <summary>Gets the source of the Workspaces page.</summary>
    internal FakeWorkspacesSource WorkspacesSource { get; } = new();

    /// <summary>Gets the source of the Worktrees page.</summary>
    internal FakeWorktreesSource WorktreesSource { get; } = new();

    /// <summary>Gets the source of the Ignore page.</summary>
    internal FakeIgnoreSource IgnoreSource { get; } = new();

    /// <summary>Gets the host of the Remote page.</summary>
    internal ScriptedRemoteHost RemoteHost { get; } = new();

    /// <summary>Gets the clock of the Cloud page.</summary>
    internal ManualTimeProvider Clock { get; } = new();

    /// <summary>Gets the host of the Cloud page.</summary>
    internal FakeCloudAgentsHost CloudHost { get; }

    /// <summary>Gets how many times the About button of the General page was pressed.</summary>
    internal int AboutOpened { get; private set; }

    /// <summary>Gets the values the Runtime page announced as saved, in order.</summary>
    internal List<RuntimeSettingsValues> RuntimeSaved { get; } = [];

    /// <summary>Gets the Runtime page.</summary>
    internal RuntimePage Runtime { get; }

    /// <summary>Gets the Models page.</summary>
    internal ModelsSettingsPage Models { get; }

    /// <summary>Gets the General page.</summary>
    internal GeneralPage General { get; }

    /// <summary>Gets the Display page.</summary>
    internal DisplaySettingsPage Display { get; }

    /// <summary>Gets the Agents page.</summary>
    internal AgentsPage Agents { get; }

    /// <summary>Gets the Run mode page.</summary>
    internal RunModeSettingsPage RunMode { get; }

    /// <summary>Gets the Tab page.</summary>
    internal TabPage Tab { get; }

    /// <summary>Gets the Plugins page.</summary>
    internal PluginsPage Plugins { get; }

    /// <summary>Gets the Rules page.</summary>
    internal RulesSettingsPage Rules { get; }

    /// <summary>Gets the Skills page.</summary>
    internal SkillsSettingsPage Skills { get; }

    /// <summary>Gets the Commands page.</summary>
    internal CommandsSettingsPage Commands { get; }

    /// <summary>Gets the Hooks page.</summary>
    internal HooksSettingsPage Hooks { get; }

    /// <summary>Gets the MCP page.</summary>
    internal McpSettingsPage Mcp { get; }

    /// <summary>Gets the Workspaces page.</summary>
    internal WorkspacesSettingsPage Workspaces { get; }

    /// <summary>Gets the Worktrees page.</summary>
    internal WorktreesSettingsPage Worktrees { get; }

    /// <summary>Gets the Ignore page.</summary>
    internal IgnoreSettingsPage Ignore { get; }

    /// <summary>Gets the Remote page.</summary>
    internal RemoteEnvironmentsSettingsPage Remote { get; }

    /// <summary>Gets the host of the Containers and WSL section of the Remote page.</summary>
    internal ScriptedContainerHost ContainerHost { get; } = new();

    /// <summary>Gets the lister of running containers of the Remote page.</summary>
    internal ScriptedContainerLister ContainerLister { get; } = new();

    /// <summary>Gets the manager of the Automations page.</summary>
    internal ScriptedAutomationsManager AutomationsManager { get; } = new();

    /// <summary>Gets the Automations page.</summary>
    internal AutomationsSettingsPage Automations { get; }

    /// <summary>Gets the Cloud page.</summary>
    internal CloudSettingsPage Cloud { get; }

    /// <summary>Gets the model behind the Cloud page; the fixture disposes it.</summary>
    internal CloudAgentsViewModel CloudModel { get; }

    /// <summary>Gets the Privacy page.</summary>
    internal PrivacySettingsPage Privacy { get; }

    /// <summary>Gets the Network page.</summary>
    internal NetworkSettingsPage Network { get; }

    /// <summary>Gets the Keyboard page.</summary>
    internal KeyboardSettingsPage Keyboard { get; }

    /// <summary>Gets the window model.</summary>
    internal SettingsWindowModel Model { get; }

    /// <summary>Gets the window.</summary>
    internal SettingsWindow Window { get; }

    /// <summary>
    /// Builds the fixture on the dispatcher thread of a headless session. The sources are configured first, so a test can script a failure or
    /// a gate before anything loads.
    /// </summary>
    /// <param name="cancellationToken">Cancels the loads.</param>
    /// <param name="configure">Scripts the sources. Null keeps the defaults.</param>
    /// <param name="load">True to load every page before the window is shown; false to leave the pages loading.</param>
    /// <returns>The fixture. The caller disposes it.</returns>
    internal static async Task<SettingsFixture> OpenAsync(CancellationToken cancellationToken, Action<SettingsFixture>? configure = null, bool load = true)
    {
        var fixture = new SettingsFixture();
        configure?.Invoke(fixture);
        if (load)
        {
            await fixture.Model.LoadAllAsync(cancellationToken).ConfigureAwait(true);
        }

        fixture.Window.Show();
        fixture.Settle();
        return fixture;
    }

    /// <summary>Lets the dispatcher and layout settle after a change.</summary>
    internal void Settle() => SettingsTree.SettleSafe(Window);

    /// <summary>Selects a section the way the user does and lets the window settle.</summary>
    /// <param name="section">The section to show.</param>
    /// <returns>The view of the page.</returns>
    internal Control Show(SettingsSection section)
    {
        Model.SelectSection(section);
        Settle();
        return Window.CurrentView ?? throw new InvalidOperationException("The window shows no page view.");
    }

    /// <summary>Drops every unsaved edit and closes the window.</summary>
    public void Dispose()
    {
        foreach (var page in Model.Pages)
        {
            page.Discard();
        }

        _ = Model.RequestClose();
        Dispatcher.UIThread.RunJobs();
        CloudModel.Dispose();
    }
}
