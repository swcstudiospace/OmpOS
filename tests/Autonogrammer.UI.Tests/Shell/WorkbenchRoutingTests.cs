using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.UI.Components.Settings;
using Autonogrammer.UI.Components.Shell;
using Autonogrammer.UI.Tests.Fakes;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The workbench is addressable: the Agents view, the Kanban view, one session, the Settings dialog and About each have an address, a title and one heading, and the tabs, the shortcuts, the rail and Back and Forward move between them.</summary>
public sealed class WorkbenchRoutingTests : UiTestContext
{
    private const string FocusBySelector = "Blazor._internal.domWrapper.focusBySelector";

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private string Path => Navigation.ToBaseRelativePath(Navigation.Uri);

    private static PanelHarness TwoAgents() => PanelHarness.With(
        ("a", SessionLifecycle.Running, "Alpha"),
        ("b", SessionLifecycle.Completed, "Beta"));

    private IRenderedComponent<WorkbenchView> RenderAt(string path, PanelHarness harness)
    {
        Navigation.NavigateTo(path);
        var settings = new SettingsWindowModel([new StubSettingsPage()]);
        return Render<WorkbenchView>(parameters => parameters
            .Add(p => p.Panel, harness.Panel)
            .Add(p => p.Board, harness.Board)
            .Add(p => p.Settings, settings));
    }

    private List<JSRuntimeInvocation> HeadingFocusCalls() =>
        JSInterop.Invocations.Where(static call => string.Equals(call.Identifier, FocusBySelector, StringComparison.Ordinal)).ToList();

    /// <summary>Every route has its own document title, most specific first, announced through the page title.</summary>
    [Theory]
    [InlineData("", "Agents - Autonogrammer")]
    [InlineData("kanban", "Kanban - Autonogrammer")]
    [InlineData("about", "About - Autonogrammer")]
    [InlineData("settings", "Settings - Autonogrammer")]
    [InlineData("settings/general", "General - Settings - Autonogrammer")]
    [InlineData("sessions/a", "Alpha - Agents - Autonogrammer")]
    [InlineData("sessions/zzz", "Session not found - Autonogrammer")]
    public void EveryRouteHasItsOwnTitle(string path, string title)
    {
        using var harness = TwoAgents();
        var head = Render<HeadOutlet>();

        RenderAt(path, harness);

        head.WaitForAssertion(() => head.Find("title").TextContent.ShouldBe(title));
    }

    /// <summary>Every view renders exactly one h1, and the top bar wordmark is a link home, not a heading.</summary>
    [Theory]
    [InlineData("", "Agents")]
    [InlineData("kanban", "Kanban")]
    [InlineData("about", "About Autonogrammer")]
    [InlineData("sessions/a", "Agents")]
    [InlineData("sessions/zzz", "Session not found")]
    [InlineData("settings", "Agents")]
    public void EveryViewHasExactlyOneHeading(string path, string heading)
    {
        using var harness = TwoAgents();

        var cut = RenderAt(path, harness);

        cut.WaitForAssertion(() => cut.FindAll("h1").Select(static element => element.TextContent).ShouldBe([heading]));
        cut.FindAll("header h1").ShouldBeEmpty();
        var wordmark = cut.Find("header a.autonogrammer-wordmark");
        wordmark.GetAttribute("href").ShouldBe("/");
        wordmark.TextContent.ShouldBe("Autonogrammer");
        foreach (var element in cut.FindAll("h1"))
        {
            element.GetAttribute("tabindex").ShouldBe("-1", "the heading takes programmatic focus after a navigation");
        }
    }

    /// <summary>A session address selects that session once the catalogue has it.</summary>
    [Fact]
    public async Task ASessionAddressSelectsTheSession()
    {
        using var harness = TwoAgents();

        var cut = RenderAt("sessions/b", harness);

        await cut.WaitForAssertionAsync(() => harness.Panel.Selected.ShouldNotBeNull().Id.ShouldBe("b")).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.Find("h2.autonogrammer-thread-title").TextContent.ShouldBe("Beta")).ConfigureAwait(true);
        cut.FindAll("[data-state='not-found']").ShouldBeEmpty();
    }

    /// <summary>An address nobody owns says so, names the way home and takes the focus.</summary>
    [Fact]
    public async Task AnUnknownSessionAddressSaysSoAndTakesTheFocus()
    {
        using var harness = TwoAgents();

        var cut = RenderAt("sessions/zzz", harness);

        await cut.WaitForAssertionAsync(() => cut.Find("[data-state='not-found'] h1").TextContent.ShouldBe("Session not found")).ConfigureAwait(true);
        cut.Find("[data-state='not-found'] a").GetAttribute("href").ShouldBe("/");
        await cut.WaitForAssertionAsync(() => HeadingFocusCalls().ShouldNotBeEmpty("the heading takes the focus")).ConfigureAwait(true);
        HeadingFocusCalls()[^1].Arguments[0].ShouldBe("main h1");
        cut.FindAll(".autonogrammer-thread").ShouldBeEmpty();
        cut.Find("a.autonogrammer-skip-link").GetAttribute("href").ShouldBe("#main");
    }

    /// <summary>The tabs move the address, and the address moves the tabs: Back and Forward (a navigation the shell did not start) change the view and focus its heading.</summary>
    [Fact]
    public async Task TheTabsAndTheAddressFollowEachOther()
    {
        using var harness = TwoAgents();
        var cut = RenderAt(string.Empty, harness);

        await cut.Find($"#{ViewTabs.KanbanTabId}").ClickAsync().ConfigureAwait(true);

        Path.ShouldBe("kanban");
        cut.Find($"#{ViewTabs.KanbanTabId}").GetAttribute("aria-selected").ShouldBe("true");
        await cut.Find($"#{ViewTabs.AgentsTabId}").ClickAsync().ConfigureAwait(true);
        Path.ShouldBeEmpty();
        cut.Find($"#{ViewTabs.AgentsTabId}").GetAttribute("aria-selected").ShouldBe("true");
        HeadingFocusCalls().ShouldBeEmpty("a tab keeps the focus on the tab");

        Navigation.NavigateTo("about");

        await cut.WaitForAssertionAsync(() => cut.Find("h1").TextContent.ShouldBe("About Autonogrammer")).ConfigureAwait(true);
        cut.FindAll("[role='tab'][aria-selected='true']").ShouldBeEmpty("About is neither tab");
        await cut.WaitForAssertionAsync(() => HeadingFocusCalls().ShouldNotBeEmpty()).ConfigureAwait(true);

        Navigation.NavigateTo("kanban");

        await cut.WaitForAssertionAsync(() => cut.Find($"#{ViewTabs.KanbanTabId}").GetAttribute("aria-selected").ShouldBe("true")).ConfigureAwait(true);
        cut.Find("h1").TextContent.ShouldBe("Kanban");
    }

    /// <summary>Ctrl+Shift+A, Ctrl+Shift+K and Ctrl+Comma move the address.</summary>
    [Fact]
    public async Task TheShortcutsNavigate()
    {
        using var harness = TwoAgents();
        var cut = RenderAt(string.Empty, harness);
        var host = cut.FindComponent<ShortcutHost>().Instance;

        await cut.InvokeAsync(() => host.HandleShortcutAsync(ShortcutHost.Kanban)).ConfigureAwait(true);
        Path.ShouldBe("kanban");

        await cut.InvokeAsync(() => host.HandleShortcutAsync(ShortcutHost.Agents)).ConfigureAwait(true);
        Path.ShouldBeEmpty();

        await cut.InvokeAsync(() => host.HandleShortcutAsync(ShortcutHost.Settings)).ConfigureAwait(true);
        Path.ShouldBe("settings");
        await cut.WaitForAssertionAsync(() => cut.FindComponent<SettingsDialog>().Instance.IsOpen.ShouldBeTrue()).ConfigureAwait(true);
    }

    /// <summary>The rail's Settings button opens the dialog at /settings, and closing it returns to the view that was showing, so Back and the address agree with the screen.</summary>
    [Fact]
    public async Task ClosingSettingsReturnsToThePreviousView()
    {
        using var harness = TwoAgents();
        var cut = RenderAt("kanban", harness);

        await cut.Find("nav button[aria-label='Settings']").ClickAsync().ConfigureAwait(true);

        Path.ShouldBe("settings");
        await cut.WaitForAssertionAsync(() => cut.FindComponent<SettingsDialog>().Instance.IsOpen.ShouldBeTrue()).ConfigureAwait(true);
        cut.Find("h1").TextContent.ShouldBe("Kanban", "the view under the dialog keeps its heading");

        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-settings-header button").ShouldNotBeNull()).ConfigureAwait(true);
        await cut.Find(".autonogrammer-settings-header button").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => Path.ShouldBe("kanban")).ConfigureAwait(true);
        cut.FindComponent<SettingsDialog>().Instance.IsOpen.ShouldBeFalse();
    }

    /// <summary>A settings address opens the dialog over the default view, and closing it goes home.</summary>
    [Fact]
    public async Task ASettingsAddressOpensTheDialogOverTheHomeView()
    {
        using var harness = TwoAgents();
        var cut = RenderAt("settings/general", harness);

        cut.FindComponent<SettingsDialog>().Instance.IsOpen.ShouldBeTrue();
        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-settings-header button").ShouldNotBeNull()).ConfigureAwait(true);

        await cut.Find(".autonogrammer-settings-header button").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => Path.ShouldBeEmpty()).ConfigureAwait(true);
    }

    /// <summary>Opening an agent in the rail puts its address in the bar.</summary>
    [Fact]
    public async Task OpeningAnAgentPutsItsAddressInTheBar()
    {
        using var harness = TwoAgents();
        var cut = RenderAt(string.Empty, harness);

        var rows = cut.FindAll("button.autonogrammer-agent-row");
        var beta = rows.Single(static row => row.TextContent.Contains("Beta", StringComparison.Ordinal));
        await beta.ClickAsync().ConfigureAwait(true);

        Path.ShouldBe("sessions/b");
        await cut.WaitForAssertionAsync(() => cut.Find("h2.autonogrammer-thread-title").TextContent.ShouldBe("Beta")).ConfigureAwait(true);
    }

    /// <summary>The footer links to the About view.</summary>
    [Fact]
    public void TheFooterLinksToAbout()
    {
        using var harness = TwoAgents();

        var cut = RenderAt(string.Empty, harness);

        cut.Find("footer a.autonogrammer-footer-about").GetAttribute("href").ShouldBe("about");
    }
}
