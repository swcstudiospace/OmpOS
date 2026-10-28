using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.UI.Components.Rail;
using OmpOS.UI.Components.Shell;
using OmpOS.UI.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The agent rail draws the Agent Panel view model: grouped rows with a status glyph and words, roving keyboard navigation, close with confirmation, and the loading, empty, no-match and error states (PX-09).</summary>
public sealed class AgentRailTests : UiTestContext
{
    private const string KeysModule = "./_content/OmpOS.UI/scripts/ompos-keys.js";
    private const string ShellModule = "./_content/OmpOS.UI/scripts/ompos-shell.js";
    private const string RowSelector = "button.ompos-agent-row";

    private static PanelHarness ThreeAgents() => PanelHarness.With(
        ("a", SessionLifecycle.Running, "Alpha"),
        ("b", SessionLifecycle.Completed, "Beta"),
        ("c", SessionLifecycle.Created, "Gamma"));

    private IRenderedComponent<AgentRail> RenderRail(PanelHarness harness, Action<ComponentParameterCollectionBuilder<AgentRail>>? configure = null) =>
        Render<AgentRail>(parameters =>
        {
            parameters.Add(p => p.Panel, harness.Panel);
            configure?.Invoke(parameters);
        });

    /// <summary>Rows are grouped under the Presentation headers, and each row names itself with the Presentation accessible name, shows the status in words and draws a decorative glyph.</summary>
    [Fact]
    public void RowsAreGroupedAndNameTheirStatusInWords()
    {
        using var harness = ThreeAgents();

        var cut = RenderRail(harness);

        cut.FindAll("h3.ompos-agent-group-header").Select(static header => header.TextContent).ShouldBe(["Running", "Idle", "Finished"]);
        var rows = cut.FindAll(RowSelector);
        rows.Count.ShouldBe(3);
        for (var index = 0; index < rows.Count; index++)
        {
            var expected = harness.Panel.Catalog.Rows[index];
            // The name lists the words the row shows, in the order it shows them (WCAG 2.5.3 label in name; axe label-content-name-mismatch).
            var time = expected.IsStale && expected.QuietText is not null ? expected.QuietText : expected.LastActivityText;
            rows[index].GetAttribute("aria-label").ShouldStartWith(expected.Title + ", " + time + ", " + expected.StatusText, Case.Sensitive);
            rows[index].QuerySelector(".ompos-agent-status")!.TextContent.ShouldBe(expected.StatusText);
            rows[index].QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
        }
    }

    /// <summary>A click opens the session in the catalog, marks the row current and reports the identifier.</summary>
    [Fact]
    public async Task ClickOpensTheSessionAndMarksItCurrent()
    {
        using var harness = ThreeAgents();
        string? opened = null;
        var cut = RenderRail(harness, parameters => parameters.Add(p => p.OnOpened, (string id) => opened = id));

        await cut.FindAll(RowSelector)[1].ClickAsync().ConfigureAwait(true);

        // The rail lists Running (a), then Idle (c, Created), then Finished (b, Completed): the second row is Gamma.
        opened.ShouldBe("c");
        harness.Panel.Catalog.SelectedId.ShouldBe("c");
        await cut.WaitForAssertionAsync(() => cut.Find("button[aria-current='true']").GetAttribute("data-row-id").ShouldBe("c")).ConfigureAwait(true);
        cut.FindAll("button[aria-current='true']").Count.ShouldBe(1);
    }

    /// <summary>Exactly one row is in the tab order; Up, Down, Home and End move the focus between rows without opening them.</summary>
    [Fact]
    public async Task ArrowKeysMoveTheFocusWithARovingTabindex()
    {
        using var harness = ThreeAgents();
        var keys = JSInterop.SetupModule(KeysModule);
        var cut = RenderRail(harness);
        cut.FindAll("button.ompos-agent-row[tabindex='0']").Count.ShouldBe(1);
        cut.FindAll(RowSelector)[0].GetAttribute("tabindex").ShouldBe("0");

        await cut.FindAll(RowSelector)[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }).ConfigureAwait(true);

        keys.VerifyInvoke("focusItem").Arguments[1].ShouldBe("c");
        cut.FindAll(RowSelector).Single(static row => string.Equals(row.GetAttribute("tabindex"), "0", StringComparison.Ordinal)).GetAttribute("data-row-id").ShouldBe("c");
        harness.Panel.Catalog.SelectedId.ShouldBeNull("moving the focus does not open an agent");

        await cut.FindAll(RowSelector)[1].KeyDownAsync(new KeyboardEventArgs { Key = "End" }).ConfigureAwait(true);
        cut.FindAll(RowSelector).Single(static row => string.Equals(row.GetAttribute("tabindex"), "0", StringComparison.Ordinal)).GetAttribute("data-row-id").ShouldBe("b");

        await cut.FindAll(RowSelector)[0].KeyDownAsync(new KeyboardEventArgs { Key = "Home" }).ConfigureAwait(true);
        cut.FindAll(RowSelector).Single(static row => string.Equals(row.GetAttribute("tabindex"), "0", StringComparison.Ordinal)).GetAttribute("data-row-id").ShouldBe("a");

        await cut.FindAll(RowSelector)[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" }).ConfigureAwait(true);
        cut.FindAll(RowSelector).Single(static row => string.Equals(row.GetAttribute("tabindex"), "0", StringComparison.Ordinal)).GetAttribute("data-row-id").ShouldBe("a");
    }

    /// <summary>Delete on a running agent asks first; keeping it leaves the agent open, confirming closes it and the close is announced.</summary>
    [Fact]
    public async Task DeleteOnARunningAgentAsksBeforeClosing()
    {
        using var harness = ThreeAgents();
        JSInterop.SetupModule(KeysModule);
        var cut = RenderRail(harness);

        await cut.FindAll(RowSelector)[0].KeyDownAsync(new KeyboardEventArgs { Key = "Delete" }).ConfigureAwait(true);

        var bar = cut.Find(".ompos-confirm-bar[data-kind='close']");
        bar.GetAttribute("aria-label").ShouldBe("Confirm close");
        bar.TextContent.ShouldContain(AgentPanelCopy.CloseConfirmation("Alpha"), Case.Sensitive);
        harness.Host.Closed.ShouldBeEmpty();

        await cut.Find(".ompos-confirm-bar button:not(.ompos-btn--primary)").ClickAsync().ConfigureAwait(true);
        cut.FindAll(".ompos-confirm-bar").ShouldBeEmpty();
        harness.Host.Closed.ShouldBeEmpty();

        await cut.FindAll(RowSelector)[0].KeyDownAsync(new KeyboardEventArgs { Key = "Delete" }).ConfigureAwait(true);
        await cut.Find(".ompos-confirm-bar .ompos-btn--primary").ClickAsync().ConfigureAwait(true);

        harness.Host.Closed.ShouldBe(["a"]);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-sr-only[role='status']").TextContent.ShouldContain("Agent \"Alpha\" closed.", Case.Sensitive)).ConfigureAwait(true);
    }

    /// <summary>Delete on an idle agent closes it at once, and a refused close shows the reason in an alert.</summary>
    [Fact]
    public async Task DeleteOnAnIdleAgentClosesItAndAFailureIsAnAlert()
    {
        using var harness = ThreeAgents();
        JSInterop.SetupModule(KeysModule);
        var cut = RenderRail(harness);

        // The rail lists Running (a), then Idle (c, Created), then Finished (b): focusing the second row makes Gamma the row Delete acts on.
        await cut.FindAll(RowSelector)[1].FocusAsync().ConfigureAwait(true);
        await cut.FindAll(RowSelector)[1].KeyDownAsync(new KeyboardEventArgs { Key = "Delete" }).ConfigureAwait(true);
        cut.FindAll(".ompos-confirm-bar").ShouldBeEmpty();
        harness.Host.Closed.ShouldBe(["c"]);

        harness.Host.CloseFailure = new InvalidOperationException("engine busy");
        await cut.FindAll(RowSelector)[0].FocusAsync().ConfigureAwait(true);
        await cut.FindAll(RowSelector)[0].KeyDownAsync(new KeyboardEventArgs { Key = "Delete" }).ConfigureAwait(true);
        await cut.Find(".ompos-confirm-bar .ompos-btn--primary").ClickAsync().ConfigureAwait(true);

        cut.Find("[role='alert']").TextContent.ShouldContain("Could not close the agent. engine busy.", Case.Sensitive);
    }

    /// <summary>The filter narrows the rows; a filter nothing matches shows the no-match copy, and Escape clears it.</summary>
    [Fact]
    public async Task FilterNarrowsTheRowsAndEscapeClearsIt()
    {
        using var harness = ThreeAgents();
        var cut = RenderRail(harness);
        var filter = cut.Find("input[aria-label='Filter agents']");

        await filter.InputAsync(new ChangeEventArgs { Value = "alp" }).ConfigureAwait(true);
        cut.FindAll(RowSelector).Select(static row => row.GetAttribute("data-row-id")).ShouldBe(["a"]);

        await filter.InputAsync(new ChangeEventArgs { Value = "zzz" }).ConfigureAwait(true);
        cut.FindAll(RowSelector).ShouldBeEmpty();
        cut.Find("[data-state='no-match']").TextContent.ShouldContain(harness.Panel.Catalog.NoMatchHeading, Case.Sensitive);

        await cut.Find("input[aria-label='Filter agents']").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);
        harness.Panel.Catalog.Filter.ShouldBeEmpty();
        cut.FindAll(RowSelector).Count.ShouldBe(3);
    }

    /// <summary>With no agent the rail says so and lists nothing.</summary>
    [Fact]
    public void AnEmptyCatalogShowsTheEmptyState()
    {
        using var harness = PanelHarness.With();

        var cut = RenderRail(harness);

        cut.Find("[data-state='empty']").TextContent.ShouldContain(AgentPanelCopy.EmptyRailHeading, Case.Sensitive);
        cut.FindAll("ul").ShouldBeEmpty();
    }

    /// <summary>While the panel starts, the rail shows a polite loading status.</summary>
    [Fact]
    public void WhileTheRailLoadsItShowsAStatus()
    {
        using var harness = ThreeAgents();
        using var scope = harness.Panel.BeginInitialising();

        var cut = RenderRail(harness);

        var state = cut.Find("[data-state='loading']");
        state.GetAttribute("role").ShouldBe("status");
        state.TextContent.ShouldContain(AgentPanelCopy.RailLoading, Case.Sensitive);
    }

    /// <summary>A failed load shows the reason in an alert and a Retry control that asks the host to reload.</summary>
    [Fact]
    public async Task ALoadFailureShowsTheReasonAndRetries()
    {
        using var harness = ThreeAgents();
        harness.Panel.ReportLoadFailure("offline");
        var cut = RenderRail(harness);

        cut.Find("[data-state='error'] [role='alert']").TextContent.ShouldContain("offline", Case.Sensitive);
        await cut.Find($"button[aria-label='{AgentPanelCopy.RetryLoadingName}']").ClickAsync().ConfigureAwait(true);

        harness.Host.ReloadCount.ShouldBe(1);
    }

    /// <summary>New conversation starts and selects an agent and closes the open drawer, so a phone user lands on the thread.</summary>
    [Fact]
    public async Task NewConversationStartsAnAgentAndClosesTheDrawer()
    {
        using var harness = PanelHarness.With();
        JSInterop.SetupModule(ShellModule);
        JSInterop.SetupModule(KeysModule);
        var cut = Render<AppShell>(parameters => parameters
            .Add<AgentRail>(p => p.Rail, rail => rail.Add(r => r.Panel, harness.Panel)));
        await cut.Find("button.ompos-topbar-menu").ClickAsync().ConfigureAwait(true);
        cut.Instance.IsDrawerOpen.ShouldBeTrue();

        var button = cut.Find("button[aria-label='New conversation']");
        button.TextContent.Trim().ShouldBe("New conversation");
        button.ClassList.ShouldContain("ompos-btn", StringComparer.Ordinal);
        button.QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
        await button.ClickAsync().ConfigureAwait(true);

        harness.Host.Created.ShouldBe(["new-1"]);
        harness.Panel.Catalog.SelectedId.ShouldBe("new-1");
        cut.Instance.IsDrawerOpen.ShouldBeFalse();
    }

    /// <summary>Opening an agent inside the shell closes the open drawer.</summary>
    [Fact]
    public async Task OpeningAnAgentClosesTheOpenDrawer()
    {
        using var harness = ThreeAgents();
        JSInterop.SetupModule(ShellModule);
        JSInterop.SetupModule(KeysModule);
        var cut = Render<AppShell>(parameters => parameters
            .Add<AgentRail>(p => p.Rail, rail => rail.Add(r => r.Panel, harness.Panel)));
        await cut.Find("button.ompos-topbar-menu").ClickAsync().ConfigureAwait(true);

        await cut.FindAll(RowSelector)[0].ClickAsync().ConfigureAwait(true);

        harness.Panel.Catalog.SelectedId.ShouldBe("a");
        cut.Instance.IsDrawerOpen.ShouldBeFalse();
    }

    /// <summary>New omp terminal starts a terminal session; Settings reports to the page.</summary>
    [Fact]
    public async Task TerminalAndSettingsButtonsReachTheirHandlers()
    {
        using var harness = PanelHarness.With();
        var settings = 0;
        var terminals = 0;
        var cut = RenderRail(harness, parameters => parameters
            .Add(p => p.OnOpenSettings, () => settings++)
            .Add(p => p.OnNewTerminalCompleted, () => terminals++));

        await cut.Find($"button[aria-label='{AgentPanelCopy.NewTerminal}']").ClickAsync().ConfigureAwait(true);
        await cut.Find("button[aria-label='Settings']").ClickAsync().ConfigureAwait(true);

        harness.Host.CreatedTerminals.Count.ShouldBe(1);
        terminals.ShouldBe(1);
        settings.ShouldBe(1);
    }

    /// <summary>A lost agent that is selected offers Resume on its row, and the command reaches the host.</summary>
    [Fact]
    public async Task ASelectedLostAgentOffersResume()
    {
        using var harness = PanelHarness.With(("x", SessionLifecycle.Lost, "Lost one"));
        var cut = RenderRail(harness);
        cut.FindAll("button").Where(static button => string.Equals(button.TextContent, AgentPanelCopy.ResumeAgent, StringComparison.Ordinal)).ShouldBeEmpty("only the selected row offers actions");

        await cut.Find(RowSelector).ClickAsync().ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-agent-actions").ShouldNotBeNull()).ConfigureAwait(true);
        await cut.FindAll(".ompos-agent-actions button").Single(static button => string.Equals(button.TextContent, AgentPanelCopy.ResumeAgent, StringComparison.Ordinal)).ClickAsync().ConfigureAwait(true);

        harness.Host.Resumes.ShouldBe(1);
    }

    /// <summary>Every control of the rail has an accessible name, and the live region is polite.</summary>
    [Fact]
    public void EveryControlHasAnAccessibleNameAndTheLiveRegionIsPolite()
    {
        using var harness = ThreeAgents();

        var cut = RenderRail(harness);

        foreach (var button in cut.FindAll("button"))
        {
            (button.GetAttribute("aria-label") ?? button.TextContent).Trim().ShouldNotBeEmpty();
        }

        cut.Find("input").GetAttribute("aria-label").ShouldBe("Filter agents");
        cut.Find(".ompos-sr-only[role='status']").GetAttribute("aria-live").ShouldBe("polite");
        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("[style]").ShouldBeEmpty();
    }
}
