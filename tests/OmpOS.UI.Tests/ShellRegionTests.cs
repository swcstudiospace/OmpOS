using Bunit;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.UI.Components.Shell;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The page regions the workbench adds to the shell: the permanent demo banner, the Agents and Kanban view tabs, and the global shortcuts (PX-09).</summary>
public sealed class ShellRegionTests : UiTestContext
{
    private const string KeysModule = "./_content/OmpOS.UI/scripts/ompos-keys.js";
    private const string FocusIdentifier = "Blazor._internal.domWrapper.focus";

    /// <summary>The demo banner is a status with the fixed words and no control: it cannot be dismissed.</summary>
    [Fact]
    public void TheDemoBannerIsPermanentAndSaysWhatTheDemoIs()
    {
        var cut = Render<DemoBanner>();

        var banner = cut.Find("div.ompos-demo-banner");
        banner.GetAttribute("role").ShouldBe("status");
        banner.TextContent.ShouldBe("Demo: simulated engine, no real agent, no network");
        banner.TextContent.ShouldBe(DemoBanner.Text);
        cut.FindAll("button, a, [tabindex]").ShouldBeEmpty();
    }

    /// <summary>The tabs are a tablist with one selected tab in the tab order, each naming the shortcut that selects it and the panel it controls.</summary>
    [Fact]
    public void TheTabsAreATablistWithOneTabStop()
    {
        var cut = Render<ViewTabs>();

        cut.Find("[role='tablist']").GetAttribute("aria-label").ShouldBe("Views");
        var tabs = cut.FindAll("[role='tab']");
        tabs.Select(static tab => tab.TextContent).ShouldBe(["Agents", "Kanban"]);
        tabs.Select(static tab => tab.GetAttribute("aria-selected")).ShouldBe(["true", "false"]);
        tabs.Select(static tab => tab.GetAttribute("tabindex")).ShouldBe(["0", "-1"]);
        tabs.Select(static tab => tab.GetAttribute("aria-controls")).ShouldBe([ViewTabs.PanelId, ViewTabs.PanelId]);
        tabs.Select(static tab => tab.GetAttribute("aria-keyshortcuts")).ShouldBe(["Control+Shift+A", "Control+Shift+K"]);
    }

    /// <summary>A click reports the chosen view; the arrow keys, Home and End move the selection and the focus, and choosing the selected tab reports nothing.</summary>
    [Fact]
    public async Task ChoosingATabReportsTheViewAndKeysMoveTheFocus()
    {
        var chosen = new List<string>();
        var cut = Render<ViewTabs>(parameters => parameters
            .Add(p => p.Selected, ViewTabs.AgentsView)
            .Add(p => p.SelectedChanged, (string view) => chosen.Add(view)));
        var tabIds = cut.FindAll("[role='tab']").Select(static tab => tab.InitialReferenceId()).ToList();

        await cut.FindAll("[role='tab']")[0].ClickAsync().ConfigureAwait(true);
        chosen.ShouldBeEmpty();
        await cut.FindAll("[role='tab']")[1].ClickAsync().ConfigureAwait(true);
        chosen.ShouldBe([ViewTabs.KanbanView]);

        await cut.Find("[role='tablist']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" }).ConfigureAwait(true);
        chosen.ShouldBe([ViewTabs.KanbanView, ViewTabs.KanbanView]);
        JSInterop.Invocations[FocusIdentifier][^1].ReferenceIdOf().ShouldBe(tabIds[1]);

        cut.Render(parameters => parameters.Add(p => p.Selected, ViewTabs.KanbanView));
        await cut.Find("[role='tablist']").KeyDownAsync(new KeyboardEventArgs { Key = "Home" }).ConfigureAwait(true);
        chosen[^1].ShouldBe(ViewTabs.AgentsView);
        JSInterop.Invocations[FocusIdentifier][^1].ReferenceIdOf().ShouldBe(tabIds[0]);
    }

    /// <summary>With the Kanban view selected its tab is the only tab stop.</summary>
    [Fact]
    public void TheKanbanTabIsTheTabStopWhenSelected()
    {
        var cut = Render<ViewTabs>(parameters => parameters.Add(p => p.Selected, ViewTabs.KanbanView));

        cut.FindAll("[role='tab']").Select(static tab => tab.GetAttribute("tabindex")).ShouldBe(["-1", "0"]);
        cut.FindAll("[role='tab']").Select(static tab => tab.GetAttribute("aria-selected")).ShouldBe(["false", "true"]);
    }

    /// <summary>The shortcut host registers the document listener once and forwards each shortcut identifier.</summary>
    [Fact]
    public async Task TheShortcutHostRegistersAndForwardsShortcuts()
    {
        var keys = JSInterop.SetupModule(KeysModule);
        var shortcuts = new List<string>();
        var cut = Render<ShortcutHost>(parameters => parameters.Add(p => p.OnShortcut, (string id) => shortcuts.Add(id)));
        keys.VerifyInvoke("registerShortcuts");

        await cut.InvokeAsync(() => cut.Instance.HandleShortcutAsync(ShortcutHost.Agents)).ConfigureAwait(true);
        await cut.InvokeAsync(() => cut.Instance.HandleShortcutAsync(ShortcutHost.Kanban)).ConfigureAwait(true);
        await cut.InvokeAsync(() => cut.Instance.HandleShortcutAsync(ShortcutHost.Settings)).ConfigureAwait(true);

        shortcuts.ShouldBe(["agents", "kanban", "settings"]);
    }

    /// <summary>Disposing the host releases the document listener.</summary>
    [Fact]
    public async Task DisposingTheShortcutHostReleasesTheListener()
    {
        var keys = JSInterop.SetupModule(KeysModule);
        var cut = Render<ShortcutHost>();
        keys.VerifyNotInvoke("release");

        await cut.Instance.DisposeAsync().ConfigureAwait(true);

        keys.VerifyInvoke("release");
    }

    /// <summary>The top bar shows the tabs the page passes, and the footer shows the version the page passes.</summary>
    [Fact]
    public void TheShellHostsTabsAndVersion()
    {
        JSInterop.SetupModule("./_content/OmpOS.UI/scripts/ompos-shell.js");

        var cut = Render<AppShell>(parameters => parameters
            .Add(p => p.Version, "9.9.9")
            .Add(p => p.TopBarTabs, "<span id=\"tabs-probe\">tabs</span>"));

        cut.Find("header .ompos-topbar-tabs #tabs-probe").TextContent.ShouldBe("tabs");
        cut.Find(".ompos-footer-version").TextContent.ShouldBe("OmpOS 9.9.9");
    }
}
