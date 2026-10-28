using OmpOS.Native.Settings;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Tests.Mcp;
using OmpOS.Native.Tests.Remote;
using OmpOS.Native.Tests.Safety;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The pages of the Settings window and the host that keeps its one window.</summary>
public sealed class SettingsWindowHostTests : IDisposable
{
    private readonly HeadTempDirectory _tree = new();
    private readonly List<FakeSettingsWindow> _windows = [];
    private readonly List<StubSettingsPage> _models = [];

    /// <inheritdoc />
    public void Dispose() => _tree.Dispose();

    /// <summary>
    /// With no head services the pages are in navigation order: Runtime, General, Agents, Models, Tab, Rules, Skills, Commands, Plugins, Worktrees,
    /// Ignore, Cloud (not configured), Privacy, Network, Keyboard.
    /// </summary>
    [Fact]
    public void PagesAreInNavigationOrderWithTheModelsPageFourth()
    {
        var model = SettingsWindowFactory.CreateModel(Options(withModels: true));

        model.Pages.Select(static page => page.Section).ShouldBe(
        [
            SettingsSection.Runtime,
            SettingsSection.General,
            SettingsSection.Agents,
            SettingsSection.Models,
            SettingsSection.Tab,
            SettingsSection.Rules,
            SettingsSection.Skills,
            SettingsSection.Commands,
            SettingsSection.Plugins,
            SettingsSection.Worktrees,
            SettingsSection.Ignore,
            SettingsSection.Cloud,
            SettingsSection.Privacy,
            SettingsSection.Network,
            SettingsSection.Keyboard,
        ]);
        model.SelectedSection.ShouldBe(SettingsSection.Runtime);
    }

    /// <summary>Without a Models factory the window has no Models page and every other page is still there.</summary>
    [Fact]
    public void WithoutAModelsFactoryThereIsNoModelsPage()
    {
        var model = SettingsWindowFactory.CreateModel(Options(withModels: false));

        model.Pages.Select(static page => page.Section).ShouldBe(
        [
            SettingsSection.Runtime,
            SettingsSection.General,
            SettingsSection.Agents,
            SettingsSection.Tab,
            SettingsSection.Rules,
            SettingsSection.Skills,
            SettingsSection.Commands,
            SettingsSection.Plugins,
            SettingsSection.Worktrees,
            SettingsSection.Ignore,
            SettingsSection.Cloud,
            SettingsSection.Privacy,
            SettingsSection.Network,
            SettingsSection.Keyboard,
        ]);
    }

    /// <summary>Opening the window shows it over the owner and loads every page: none is left loading.</summary>
    [Fact]
    public async Task OpeningTheWindowLoadsEveryPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = NewHost(withModels: true);

        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);

        var window = _windows.ShouldHaveSingleItem();
        window.ShowCount.ShouldBe(1);
        host.IsOpen.ShouldBeTrue();
        _models.ShouldHaveSingleItem().LoadCount.ShouldBe(1);
        foreach (var page in window.Model.Pages)
        {
            switch (page)
            {
                case EditableSettingsPage<GeneralSettingsValues> or EditableSettingsPage<AgentsSettingsValues> or TabPage or PluginsPage:
                    ((SettingsPageBase)page).State.ShouldBe(SettingsPageState.Ready, page.Title);
                    break;
                case CustomizeCatalogPage catalog:
                    catalog.State.ShouldNotBe(CustomizePageState.Loading, page.Title);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Asking while the window is open brings it forward and builds nothing: there is one window and one set of pages.</summary>
    [Fact]
    public async Task AskingAgainWhileOpenActivatesTheSameWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = NewHost(withModels: true);
        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);

        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);
        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);

        var window = _windows.ShouldHaveSingleItem();
        window.ShowCount.ShouldBe(1);
        window.ActivateCount.ShouldBe(2);
        _models.ShouldHaveSingleItem().LoadCount.ShouldBe(1);
    }

    /// <summary>After the window closed the next request builds a new window over new pages.</summary>
    [Fact]
    public async Task AfterItClosesTheNextRequestBuildsANewWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = NewHost(withModels: true);
        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);
        _windows[0].Close();
        host.IsOpen.ShouldBeFalse();

        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);

        _windows.Count.ShouldBe(2);
        _windows[1].ShowCount.ShouldBe(1);
        _windows[1].Model.ShouldNotBeSameAs(_windows[0].Model);
        _models.Count.ShouldBe(2);
        host.IsOpen.ShouldBeTrue();
    }

    /// <summary>Closing for shutdown closes the open window, and does nothing when none is open.</summary>
    [Fact]
    public async Task ClosingForShutdownClosesTheOpenWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = NewHost(withModels: false);
        host.CloseForShutdown();
        _windows.ShouldBeEmpty();
        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);

        host.CloseForShutdown();

        _windows.ShouldHaveSingleItem().ClosedForShutdown.ShouldBeTrue();
        host.IsOpen.ShouldBeFalse();
    }

    /// <summary>A window that cannot be shown is not left open: the failure reaches the caller and the next request tries again.</summary>
    [Fact]
    public async Task AWindowThatCannotBeShownIsNotLeftOpen()
    {
        var ct = TestContext.Current.CancellationToken;
        var fail = true;
        var host = new SettingsWindowHost(
            () => SettingsWindowFactory.CreateModel(Options(withModels: false)),
            model =>
            {
                var window = new FakeSettingsWindow(model) { ShowFailure = fail ? new InvalidOperationException("no display") : null };
                _windows.Add(window);
                return window;
            });

        _ = await Should.ThrowAsync<InvalidOperationException>(() => host.ShowAsync(owner: null, ct)).ConfigureAwait(true);
        host.IsOpen.ShouldBeFalse();

        fail = false;
        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);
        host.IsOpen.ShouldBeTrue();
        _windows.Count.ShouldBe(2);
    }

    /// <summary>The Plugins, Rules, Skills and Commands pages in the window read the real sources: the commands list is the shipped catalogue.</summary>
    [Fact]
    public async Task EveryPageOfTheWindowHasARealSource()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = _tree.Write("workspace/.ompos/rules/r.md", "# R\nA rule.\n");
        var host = NewHost(withModels: false);

        await host.ShowAsync(owner: null, ct).ConfigureAwait(true);

        var pages = _windows.Single().Model.Pages;
        pages.OfType<RulesSettingsPage>().Single().Entries.ShouldHaveSingleItem().Title.ShouldBe("r");
        pages.OfType<CommandsSettingsPage>().Single().Entries.ShouldNotBeEmpty();
        pages.OfType<PluginsPage>().Single().Rows.Count.ShouldBe(4);
        pages.OfType<TabPage>().Single().IsEnabled.ShouldBeFalse();
        pages.OfType<GeneralPage>().Single().Motion.ShouldBe(MotionPreference.System);
    }

    /// <summary>With every head service supplied the window has the 21 pages in the Cursor-like order.</summary>
    [Fact]
    public void EveryServiceAddsItsPageInNavigationOrder()
    {
        var clock = new ManualTimeProvider();
        using var sets = new OmpOS.Workspace.Sets.WorkspaceSetStore(_tree.PathOf("sets.json"));
        var files = new MemoryMcpFiles();
        var options = new SettingsWindowOptions
        {
            Store = new FileSettingsStore(_tree.PathOf("state")),
            WorkspaceRoot = Directory.CreateDirectory(_tree.PathOf("workspace")).FullName,
            PluginsRoot = _tree.PathOf("plugins"),
            UserHome = Directory.CreateDirectory(_tree.PathOf("home")).FullName,
            GetVariable = static _ => null,
            ModelsPageFactory = () => new StubSettingsPage(SettingsSection.Models),
            Hooks = new OmpOS.Native.Tests.Hooks.FakeHooksService(),
            Mcp = new OmpOS.Harness.Mcp.Catalog.McpServerCatalog(files, files),
            RunModes = new FakeRunModeSource(),
            Display = new OmpOS.Presentation.Commands.Handlers.DisplaySettings(new MemoryDisplayStore()),
            WorkspaceSets = sets,
            RemoteEnvironments = new ScriptedRemoteHost(),
            Cloud = new OmpOS.Presentation.Cloud.FakeCloudAgentsHost(clock),
        };

        var model = SettingsWindowFactory.CreateModel(options);

        model.Pages.Select(static page => page.Section).ShouldBe(
        [
            SettingsSection.Runtime,
            SettingsSection.General,
            SettingsSection.Display,
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
            SettingsSection.Worktrees,
            SettingsSection.Ignore,
            SettingsSection.Remote,
            SettingsSection.Cloud,
            SettingsSection.Privacy,
            SettingsSection.Network,
            SettingsSection.Keyboard,
        ]);
        options.CloudAgentsModel.ShouldNotBeNull().Dispose();
    }

    /// <summary>Asking for a section selects it in a new window and in the open one; a section the window lacks leaves the choice alone.</summary>
    [Fact]
    public async Task AskingForASectionSelectsItInANewAndAnOpenWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = NewHost(withModels: false);

        await host.ShowAsync(owner: null, ct, SettingsSection.Privacy).ConfigureAwait(true);
        var model = _windows.Single().Model;
        model.SelectedSection.ShouldBe(SettingsSection.Privacy);

        await host.ShowAsync(owner: null, ct, SettingsSection.Ignore).ConfigureAwait(true);
        model.SelectedSection.ShouldBe(SettingsSection.Ignore);

        await host.ShowAsync(owner: null, ct, SettingsSection.Hooks).ConfigureAwait(true);
        model.SelectedSection.ShouldBe(SettingsSection.Ignore);
    }

    private SettingsWindowHost NewHost(bool withModels)
    {
        var options = Options(withModels);
        return new SettingsWindowHost(
            () => SettingsWindowFactory.CreateModel(options),
            model =>
            {
                var window = new FakeSettingsWindow(model);
                _windows.Add(window);
                return window;
            });
    }

    private SettingsWindowOptions Options(bool withModels) => new()
    {
        Store = new FileSettingsStore(_tree.PathOf("state")),
        WorkspaceRoot = Directory.CreateDirectory(_tree.PathOf("workspace")).FullName,
        PluginsRoot = _tree.PathOf("plugins"),
        UserHome = Directory.CreateDirectory(_tree.PathOf("home")).FullName,
        GetVariable = static _ => null,
        ModelsPageFactory = withModels
            ? () =>
            {
                var page = new StubSettingsPage(SettingsSection.Models);
                _models.Add(page);
                return page;
            }
        : null,
    };
}
