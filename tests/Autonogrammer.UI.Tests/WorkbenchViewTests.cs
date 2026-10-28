using Bunit;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.UI.Components.Settings;
using Autonogrammer.UI.Components.Shell;
using Autonogrammer.UI.Components.Thread;
using Autonogrammer.UI.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The page composition of the web head: the shell landmarks, the rail beside the thread, the Agents and Kanban tabs, the demo banner, Settings from the rail and from Ctrl+Comma, and the panel states (PX-09).</summary>
public sealed class WorkbenchViewTests : UiTestContext
{
    private const string KeysModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-keys.js";

    private IRenderedComponent<WorkbenchView> RenderWorkbench(PanelHarness harness, Action<ComponentParameterCollectionBuilder<WorkbenchView>>? configure = null)
    {
        var settings = new SettingsWindowModel([new StubSettingsPage()]);
        return Render<WorkbenchView>(parameters =>
        {
            parameters
                .Add(p => p.Panel, harness.Panel)
                .Add(p => p.Board, harness.Board)
                .Add(p => p.Settings, settings);
            configure?.Invoke(parameters);
        });
    }

    private static PanelHarness TwoAgents() => PanelHarness.With(
        ("a", SessionLifecycle.Running, "Alpha"),
        ("b", SessionLifecycle.Completed, "Beta"));

    /// <summary>The page has the banner, navigation, main and footer landmarks, one skip link that is first in the tab order, and the thread inside main once an agent is open.</summary>
    [Fact]
    public async Task ThePageHasTheLandmarksAndTheSkipLink()
    {
        using var harness = TwoAgents();
        var cut = RenderWorkbench(harness);
        await harness.SelectAsync("a").ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.Find("main #autonogrammer-conversation").ShouldNotBeNull()).ConfigureAwait(true);

        cut.Find("header.autonogrammer-topbar").ShouldNotBeNull();
        cut.Find("nav[aria-label='Agents']").QuerySelector("button.autonogrammer-agent-row").ShouldNotBeNull();
        cut.Find("main").QuerySelector(".autonogrammer-thread").ShouldNotBeNull();
        cut.Find("footer").ShouldNotBeNull();
        var skip = cut.FindAll("a.autonogrammer-skip-link");
        skip.Count.ShouldBe(1);
        skip[0].GetAttribute("href").ShouldBe("#autonogrammer-conversation");
        skip[0].TextContent.ShouldBe("Skip to conversation");
        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("[style]").ShouldBeEmpty();
    }

    /// <summary>The licence link of the footer stays, and the workspace and version the API reported show in the top bar and the footer.</summary>
    [Fact]
    public void TheWorkspaceVersionAndLicenceShow()
    {
        using var harness = TwoAgents();
        var cut = RenderWorkbench(harness, parameters => parameters
            .Add(p => p.Workspace, new WorkspaceInfo { Name = "Demo", FolderLabel = "~/src/demo", PluginCheck = null! })
            .Add(p => p.Source, new SourceInfo { Licence = "AGPL-3.0-or-later", LicenceUrl = new Uri("https://www.gnu.org/licenses/agpl-3.0.html"), SourceUrl = new Uri("https://github.com/swcstudiospace/OmpOS"), Revision = "abc1234", Version = "0.15.0" }));

        cut.Find(".autonogrammer-workspace-label").TextContent.ShouldBe("Demo · ~/src/demo");
        cut.Find(".autonogrammer-footer-version").TextContent.ShouldContain("0.15.0", Case.Sensitive);
        cut.Find(".autonogrammer-footer-licence a").TextContent.ShouldBe("AGPL-3.0-or-later");
    }

    /// <summary>A workspace folder named like the workspace is shown once, not as "name · name" (any letter case).</summary>
    [Theory]
    [InlineData("acme-shop", "acme-shop")]
    [InlineData("acme-shop", "ACME-Shop")]
    public void AFolderNamedLikeTheWorkspaceShowsTheNameOnce(string name, string folder)
    {
        using var harness = TwoAgents();

        var cut = RenderWorkbench(harness, parameters => parameters
            .Add(p => p.Workspace, new WorkspaceInfo { Name = name, FolderLabel = folder, PluginCheck = null! }));

        cut.Find(".autonogrammer-workspace-label").TextContent.ShouldBe(name);
    }

    /// <summary>Switching the view starts the new view at the top of the document: the page is the scroll container, so it would keep the other view's offset.</summary>
    [Fact]
    public async Task SwitchingTheViewScrollsTheDocumentToTheTop()
    {
        using var harness = TwoAgents();
        var keys = JSInterop.SetupModule(KeysModule);
        var cut = RenderWorkbench(harness);

        await cut.Find($"#{ViewTabs.KanbanTabId}").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => keys.VerifyInvoke("resetScroll")).ConfigureAwait(true);
    }

    /// <summary>The demo banner shows only for a demo, inside main and ahead of the view panel.</summary>
    [Fact]
    public void TheDemoBannerShowsOnlyInADemo()
    {
        using var harness = TwoAgents();
        RenderWorkbench(harness).FindAll(".autonogrammer-demo-banner").ShouldBeEmpty();

        var cut = RenderWorkbench(harness, parameters => parameters.Add(p => p.IsDemo, true));

        cut.Find("main > .autonogrammer-demo-banner").TextContent.ShouldBe(DemoBanner.Text);
        cut.Find("main").FirstElementChild!.ClassList.ShouldContain("autonogrammer-demo-banner", StringComparer.Ordinal);
    }

    /// <summary>Choosing the Kanban tab replaces the thread with the board inside a tabpanel labelled by that tab, and the skip link targets main.</summary>
    [Fact]
    public async Task TheKanbanTabSwapsTheThreadForTheBoard()
    {
        using var harness = TwoAgents();
        var cut = RenderWorkbench(harness);
        await harness.SelectAsync("a").ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.FindAll(".autonogrammer-thread").Count.ShouldBe(1)).ConfigureAwait(true);

        await cut.Find($"#{ViewTabs.KanbanTabId}").ClickAsync().ConfigureAwait(true);

        cut.FindAll(".autonogrammer-thread").ShouldBeEmpty();
        var panel = cut.Find($"#{ViewTabs.PanelId}");
        panel.GetAttribute("role").ShouldBe("tabpanel");
        panel.GetAttribute("aria-labelledby").ShouldBe(ViewTabs.KanbanTabId);
        cut.Find("a.autonogrammer-skip-link").GetAttribute("href").ShouldBe("#main");
        cut.Find($"#{ViewTabs.KanbanTabId}").GetAttribute("aria-selected").ShouldBe("true");
    }

    /// <summary>Ctrl+Shift+K and Ctrl+Shift+A switch the view and move the focus to the selected tab; Ctrl+Comma opens Settings.</summary>
    [Fact]
    public async Task ShortcutsSwitchTheViewAndOpenSettings()
    {
        using var harness = TwoAgents();
        var cut = RenderWorkbench(harness);
        var host = cut.FindComponent<ShortcutHost>().Instance;
        var tabIds = cut.FindAll("[role='tab']").Select(static tab => tab.InitialReferenceId()).ToList();

        await cut.InvokeAsync(() => host.HandleShortcutAsync(ShortcutHost.Kanban)).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.Find($"#{ViewTabs.KanbanTabId}").GetAttribute("aria-selected").ShouldBe("true")).ConfigureAwait(true);
        JSInterop.FocusCalls()[^1].ReferenceIdOf().ShouldBe(tabIds[1]);

        await cut.InvokeAsync(() => host.HandleShortcutAsync(ShortcutHost.Agents)).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.Find($"#{ViewTabs.AgentsTabId}").GetAttribute("aria-selected").ShouldBe("true")).ConfigureAwait(true);

        cut.FindComponent<SettingsDialog>().Instance.IsOpen.ShouldBeFalse();
        await cut.InvokeAsync(() => host.HandleShortcutAsync(ShortcutHost.Settings)).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.FindComponent<SettingsDialog>().Instance.IsOpen.ShouldBeTrue()).ConfigureAwait(true);
    }

    /// <summary>The Settings button of the rail opens the dialog.</summary>
    [Fact]
    public async Task TheRailSettingsButtonOpensTheDialog()
    {
        using var harness = TwoAgents();
        var cut = RenderWorkbench(harness);

        await cut.Find("nav button[aria-label='Settings']").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindComponent<SettingsDialog>().Instance.IsOpen.ShouldBeTrue()).ConfigureAwait(true);
    }

    /// <summary>Opening an agent from the rail shows its thread and moves the focus to the transcript.</summary>
    [Fact]
    public async Task OpeningAnAgentShowsItsThreadAndFocusesTheTranscript()
    {
        var keys = JSInterop.SetupModule(KeysModule);
        using var harness = TwoAgents();
        var cut = RenderWorkbench(harness);
        cut.Find(".autonogrammer-panel-state h2").TextContent.ShouldBe("Select an agent");

        await cut.FindAll("button.autonogrammer-agent-row")[0].ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-thread h2.autonogrammer-thread-title").TextContent.ShouldBe("Alpha")).ConfigureAwait(true);
        keys.VerifyInvoke("focusId").Arguments[0].ShouldBe("autonogrammer-conversation");
    }

    /// <summary>With no agent the page offers to start the first one, and starting it opens its thread.</summary>
    [Fact]
    public async Task AnEmptyPanelOffersTheFirstAgent()
    {
        using var harness = PanelHarness.With();
        var cut = RenderWorkbench(harness);
        cut.Find(".autonogrammer-panel-state[data-state='empty'] h2").TextContent.ShouldBe(AgentPanelCopy.EmptyThreadHeading);

        await cut.Find(".autonogrammer-panel-state button").ClickAsync().ConfigureAwait(true);

        harness.Host.Created.ShouldBe(["new-1"]);
        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-thread").ShouldNotBeNull()).ConfigureAwait(true);
    }

    /// <summary>While the panel starts the main region says so, and a failed load shows the alert with Retry.</summary>
    [Fact]
    public async Task LoadingAndErrorStatesShowInTheMainRegion()
    {
        using var harness = TwoAgents();
        using (harness.Panel.BeginInitialising())
        {
            var loading = RenderWorkbench(harness);
            var state = loading.Find(".autonogrammer-panel-state[data-state='loading']");
            state.GetAttribute("role").ShouldBe("status");
            state.TextContent.ShouldContain(AgentPanelCopy.PanelLoadingHeading, Case.Sensitive);
        }

        harness.Panel.ReportLoadFailure("offline");
        var failed = RenderWorkbench(harness);
        failed.Find(".autonogrammer-panel-state[data-state='error'] [role='alert']").TextContent.ShouldContain("offline", Case.Sensitive);

        await failed.Find(".autonogrammer-panel-state[data-state='error'] button").ClickAsync().ConfigureAwait(true);

        harness.Host.ReloadCount.ShouldBe(1);
    }

    /// <summary>The host's change set and retry reach the thread of the selected agent.</summary>
    [Fact]
    public async Task TheHostsChangeSetAndRetryReachTheThread()
    {
        using var harness = TwoAgents();
        using var changes = new Autonogrammer.Presentation.Changes.ChangeSetModel(new MemoryWorkspace().With("src/a.txt", "new\n"));
        changes.Update([new Autonogrammer.Contracts.Ui.FileEditBlock { Id = "e-1", CallId = "c-1", Path = "src/a.txt", OldText = "old\n", NewText = "new\n" }]);
        var retried = new List<string>();
        var cut = RenderWorkbench(harness, parameters => parameters
            .Add(p => p.ChangesFactory, _ => changes)
            .Add(p => p.RetryAsync, id =>
            {
                retried.Add(id);
                return Task.CompletedTask;
            }));
        var session = await harness.SelectAsync("a");
        await cut.WaitForAssertionAsync(() => cut.Find("section.autonogrammer-changes").ShouldNotBeNull()).ConfigureAwait(true);
        session.Session.ApplyEvent(PanelEvents.User(1, "Do it"));
        session.Session.ApplyEvent(PanelEvents.Error(2, "boom", retryable: true));
        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-thread section > button[aria-label='Retry failed operation']").ShouldNotBeNull()).ConfigureAwait(true);

        await cut.Find(".autonogrammer-thread section > button[aria-label='Retry failed operation']").ClickAsync().ConfigureAwait(true);

        retried.ShouldBe(["a"]);
        harness.Host.Sent.ShouldBeEmpty();
        cut.FindComponent<ThreadView>().Instance.Changes.ShouldBeSameAs(changes);
    }
}
