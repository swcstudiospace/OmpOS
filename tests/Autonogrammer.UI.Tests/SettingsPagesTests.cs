using Bunit;
using Microsoft.AspNetCore.Components;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;
using Autonogrammer.UI.Components.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>Every Settings page the web head draws renders its shared page model, writes through it, and shows its loading, error, empty and saved states (PX-09).</summary>
public sealed class SettingsPagesTests : UiTestContext
{
    private const string ThemeModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-theme.js";

    /// <summary>The General page shows the loaded theme and motion as pressed buttons; the first render writes nothing to the theme module.</summary>
    [Fact]
    public async Task GeneralPageShowsTheLoadedChoicesWithoutRewritingThem()
    {
        var module = JSInterop.SetupModule(ThemeModule);
        var page = new GeneralPage(new SettingsFixtures.GeneralSource());
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        var cut = Render<GeneralSettingsView>(parameters => parameters.Add(p => p.Page, page));

        PressedLabels(cut, "#autonogrammer-setting-theme").ShouldBe(["Dark"]);
        PressedLabels(cut, "#autonogrammer-setting-motion").ShouldBe(["Follow system"]);
        cut.Find("#autonogrammer-setting-theme").GetAttribute("role").ShouldBe("group");
        cut.Find("#autonogrammer-setting-theme").GetAttribute("aria-labelledby").ShouldBe("autonogrammer-setting-theme-label");
        cut.Find("#autonogrammer-setting-theme-label").TextContent.ShouldBe(SettingsCopy.ThemeLabel);
        cut.Find(".autonogrammer-settings-privacy").TextContent.ShouldBe(SettingsCopy.PrivacyStatement);
        module.VerifyNotInvoke("setTheme");
        module.VerifyNotInvoke("setMotion");
        AssertAccessible(cut);
    }

    /// <summary>Choosing a theme or motion previews it through the theme module at once, marks the page unsaved, and Save writes the draft through the source and says Saved.</summary>
    [Fact]
    public async Task GeneralPageAppliesChoicesAtOnceAndSavesThem()
    {
        var module = JSInterop.SetupModule(ThemeModule);
        var source = new SettingsFixtures.GeneralSource();
        var page = new GeneralPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cut = Render<GeneralSettingsView>(parameters => parameters.Add(p => p.Page, page));

        await Choice(cut, "#autonogrammer-setting-theme", "Light").ClickAsync().ConfigureAwait(true);
        await Choice(cut, "#autonogrammer-setting-motion", "Reduce motion").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() =>
        {
            module.VerifyInvoke("setTheme").Arguments.ShouldBe(["light"]);
            module.VerifyInvoke("setMotion").Arguments.ShouldBe(["reduce"]);
        }).ConfigureAwait(true);
        PressedLabels(cut, "#autonogrammer-setting-theme").ShouldBe(["Light"]);
        SaveLine(cut).TextContent.ShouldBe(SettingsCopy.UnsavedBadge);
        SaveLine(cut).GetAttribute("data-state").ShouldBe("dirty");

        await cut.Find("button.autonogrammer-btn--primary").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => SaveLine(cut).TextContent.ShouldBe(SettingsWebCopy.Saved)).ConfigureAwait(true);
        source.Saved.ShouldBe([new GeneralSettingsValues(MotionPreference.Reduced, ThemePreference.Light)]);
        page.IsDirty.ShouldBeFalse();
    }

    /// <summary>Reset to defaults returns the draft to the defaults and the theme module follows it; Discard returns to the saved values.</summary>
    [Fact]
    public async Task GeneralPageResetAndDiscardPutThePreviewBack()
    {
        var module = JSInterop.SetupModule(ThemeModule);
        var source = new SettingsFixtures.GeneralSource { Values = new GeneralSettingsValues(MotionPreference.Full, ThemePreference.System) };
        var page = new GeneralPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cut = Render<GeneralSettingsView>(parameters => parameters.Add(p => p.Page, page));
        await Choice(cut, "#autonogrammer-setting-theme", "Light").ClickAsync().ConfigureAwait(true);

        await cut.FindAll(".autonogrammer-settings-footer button").Single(static button => string.Equals(button.TextContent, SettingsCopy.DiscardLabel, StringComparison.Ordinal)).ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => ThemeCalls(module).ShouldBe(["light", "system"])).ConfigureAwait(true);
        PressedLabels(cut, "#autonogrammer-setting-theme").ShouldBe(["System"]);

        await cut.FindAll(".autonogrammer-settings-footer button").Single(static button => string.Equals(button.TextContent, SettingsCopy.ResetLabel, StringComparison.Ordinal)).ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => ThemeCalls(module).ShouldBe(["light", "system", "light"])).ConfigureAwait(true);
        PressedLabels(cut, "#autonogrammer-setting-theme").ShouldBe(["Light"]);
        PressedLabels(cut, "#autonogrammer-setting-motion").ShouldBe(["Follow system"]);
    }

    /// <summary>A page that is still loading says so, in a status, and shows its controls once it is ready.</summary>
    [Fact]
    public async Task LoadingPageShowsAStatusThenItsControls()
    {
        var page = new AgentsPage(new SettingsFixtures.AgentsSource());
        var cut = Render<AgentsSettingsView>(parameters => parameters.Add(p => p.Page, page));

        cut.Find(".autonogrammer-settings-state").GetAttribute("role").ShouldBe("status");
        cut.Find(".autonogrammer-settings-state").TextContent.ShouldBe(SettingsCopy.LoadingText);
        cut.FindAll("button[role='switch']").Count.ShouldBe(0);

        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll("button[role='switch']").Count.ShouldBe(1)).ConfigureAwait(true);
    }

    /// <summary>The Agents switch is a named switch; turning it on shows the warning and the unsaved line, and Save writes it.</summary>
    [Fact]
    public async Task AgentsSwitchWritesThroughThePage()
    {
        var source = new SettingsFixtures.AgentsSource();
        var page = new AgentsPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cut = Render<AgentsSettingsView>(parameters => parameters.Add(p => p.Page, page));
        var toggle = cut.Find("button[role='switch']");
        cut.Find("label[for='" + toggle.Id + "']").TextContent.ShouldBe(SettingsCopy.AutoRunLabel);
        toggle.GetAttribute("aria-checked").ShouldBe("false");
        cut.FindAll(".autonogrammer-settings-warning").Count.ShouldBe(0);

        await toggle.ClickAsync().ConfigureAwait(true);

        cut.Find("button[role='switch']").GetAttribute("aria-checked").ShouldBe("true");
        cut.Find(".autonogrammer-settings-warning").TextContent.ShouldBe(SettingsCopy.AutoRunWarning);
        SaveLine(cut).TextContent.ShouldBe(SettingsCopy.UnsavedBadge);

        await cut.Find("button.autonogrammer-btn--primary").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => SaveLine(cut).TextContent.ShouldBe(SettingsWebCopy.Saved)).ConfigureAwait(true);
        source.Saved.ShouldBe([new AgentsSettingsValues(TerminalAutoRun: true)]);
        AssertAccessible(cut);
    }

    /// <summary>The Runtime page ties each error to its field (aria-invalid and aria-describedby), blocks the save while an entry is invalid, and saves valid entries.</summary>
    [Fact]
    public void RuntimePageAssociatesErrorsWithFieldsAndSavesValidEntries()
    {
        var saved = new List<RuntimeSettingsValues>();
        var page = new RuntimePage(new RuntimeSettingsModel());
        page.Saved += (_, args) => saved.Add(args.Values);
        var cut = Render<RuntimeSettingsView>(parameters => parameters.Add(p => p.Page, page));
        var idle = cut.Find("#autonogrammer-setting-idle-stop");
        idle.GetAttribute("value").ShouldBe("24");
        cut.Find("#autonogrammer-setting-retention").GetAttribute("value").ShouldBe("7");
        idle.HasAttribute("aria-invalid").ShouldBeFalse();

        idle.Input("abc");

        cut.WaitForAssertion(() =>
        {
            var field = cut.Find("#autonogrammer-setting-idle-stop");
            field.GetAttribute("aria-invalid").ShouldBe("true");
            field.GetAttribute("aria-describedby")!.Split(' ').ShouldContain("autonogrammer-setting-idle-stop-error", StringComparer.Ordinal);
            cut.Find("#autonogrammer-setting-idle-stop-error").TextContent.ShouldBe(RuntimeSettingsLimits.ValidateIdleStop("abc", out _));
        });

        cut.Find("button.autonogrammer-btn--primary").Click();

        saved.ShouldBeEmpty("an invalid entry can never be saved");
        SaveLine(cut).TextContent.ShouldBe(SettingsWebCopy.FixEntries);
        SaveLine(cut).GetAttribute("data-state").ShouldBe("error");

        cut.Find("#autonogrammer-setting-idle-stop").Input("48");

        cut.WaitForAssertion(() =>
        {
            cut.Find("#autonogrammer-setting-idle-stop-error").TextContent.ShouldBeEmpty();
            cut.Find("#autonogrammer-setting-idle-stop").HasAttribute("aria-invalid").ShouldBeFalse();
        });

        cut.Find("button.autonogrammer-btn--primary").Click();

        cut.WaitForAssertion(() => SaveLine(cut).TextContent.ShouldBe(SettingsWebCopy.Saved));
        saved.ShouldBe([new RuntimeSettingsValues(48, 7)]);
        AssertAccessible(cut);
    }

    /// <summary>Reset to defaults puts the defaults back into the Runtime fields.</summary>
    [Fact]
    public void RuntimeResetRestoresTheDefaults()
    {
        var page = new RuntimePage(new RuntimeSettingsModel(new RuntimeSettingsValues(100, 30)));
        var cut = Render<RuntimeSettingsView>(parameters => parameters.Add(p => p.Page, page));
        cut.Find("#autonogrammer-setting-idle-stop").GetAttribute("value").ShouldBe("100");

        cut.FindAll(".autonogrammer-settings-footer button").Single(static button => string.Equals(button.TextContent, SettingsCopy.ResetLabel, StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("#autonogrammer-setting-idle-stop").GetAttribute("value").ShouldBe("24");
            cut.Find("#autonogrammer-setting-retention").GetAttribute("value").ShouldBe("7");
        });
    }

    /// <summary>The Tab page says honestly that inline completion is not available in the web head, with no controls and no footer.</summary>
    [Fact]
    public async Task TabPageIsHonestThatItIsNotAvailableInTheWebHead()
    {
        var page = new TabPage(new SettingsFixtures.TabSource());
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        var cut = Render<TabSettingsView>(parameters => parameters.Add(p => p.Page, page));

        cut.Find("[data-availability='unavailable'] h4").TextContent.ShouldBe(SettingsWebCopy.TabUnavailableHeading);
        cut.Find("[data-availability='unavailable'] p").TextContent.ShouldBe(SettingsWebCopy.TabUnavailableBody);
        cut.FindAll("button, input").Count.ShouldBe(0);
        cut.FindAll(".autonogrammer-settings-footer").Count.ShouldBe(0);
    }

    /// <summary>The Plugins page lists each plugin with its availability in words and an icon, its version, and the note that there is no switch.</summary>
    [Fact]
    public async Task PluginsPageListsConnectionResults()
    {
        var source = new SettingsFixtures.PluginsSource
        {
            Rows =
            [
                new PluginRow("ultrathink", "Ultrathink", "Planner", "1.2.0", PluginRow.FoundAvailability, null),
                new PluginRow("swarm", "Swarm", "Orchestrator", null, PluginRow.MissingAvailability, "Not installed"),
            ],
        };
        var page = new PluginsPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        var cut = Render<PluginsSettingsView>(parameters => parameters.Add(p => p.Page, page));

        var rows = cut.FindAll(".autonogrammer-settings-list-row");
        rows.Count.ShouldBe(2);
        rows[0].QuerySelector(".autonogrammer-chip")!.TextContent.Trim().ShouldBe(SettingsCopy.PluginFoundLabel);
        rows[0].QuerySelector(".autonogrammer-chip svg")!.GetAttribute("aria-hidden").ShouldBe("true");
        rows[0].QuerySelector(".autonogrammer-settings-mono")!.TextContent.ShouldBe("1.2.0");
        rows[1].QuerySelector(".autonogrammer-chip")!.TextContent.Trim().ShouldBe(SettingsCopy.PluginMissingLabel);
        rows[1].QuerySelector(".autonogrammer-settings-mono")!.TextContent.ShouldBe(SettingsCopy.PluginVersionUnknown);
        rows[1].TextContent.ShouldContain("Not installed");
        cut.Markup.ShouldContain(SettingsCopy.PluginsAlwaysOnNote);
        cut.FindAll("button, input").Count.ShouldBe(0, "there is deliberately no switch");
    }

    /// <summary>A page that failed to load shows the reason in an alert with Try again, which loads it again.</summary>
    [Fact]
    public async Task FailedLoadShowsTheReasonAndTryAgainRecovers()
    {
        var source = new SettingsFixtures.PluginsSource { FailWith = "The plugin list is unreadable", Rows = [new PluginRow("a", "Alpha", "Role", "1.0", PluginRow.FoundAvailability, null)] };
        var page = new PluginsPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cut = Render<PluginsSettingsView>(parameters => parameters.Add(p => p.Page, page));

        cut.Find("[role='alert']").TextContent.ShouldBe("The plugin list is unreadable");
        cut.Find(".autonogrammer-settings-state h4").TextContent.ShouldBe(SettingsCopy.LoadFailedHeading);

        await cut.Find(".autonogrammer-settings-state button").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll(".autonogrammer-settings-list-row").Count.ShouldBe(1)).ConfigureAwait(true);
    }

    /// <summary>An empty plugin list says so.</summary>
    [Fact]
    public async Task EmptyPluginListSaysSo()
    {
        var page = new PluginsPage(new SettingsFixtures.PluginsSource());
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        var cut = Render<PluginsSettingsView>(parameters => parameters.Add(p => p.Page, page));

        cut.Find(".autonogrammer-settings-state").TextContent.ShouldBe(SettingsCopy.PluginsEmpty);
    }

    /// <summary>The Network page runs nothing until asked, then lists each endpoint with its status in words and an icon and announces the result.</summary>
    [Fact]
    public async Task NetworkPageRunsDiagnosticsOnRequestAndListsTheResults()
    {
        var source = new SettingsFixtures.NetworkSource
        {
            Checks =
            [
                new EndpointCheck("OpenAI", "api.openai.com", EndpointStatus.Reachable, 200, TimeSpan.FromMilliseconds(40), "HTTP 200"),
                new EndpointCheck("Mistral", "api.mistral.ai", EndpointStatus.TimedOut, null, null, "no answer"),
            ],
        };
        var page = new NetworkSettingsPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cut = Render<NetworkSettingsView>(parameters => parameters.Add(p => p.Page, page));
        cut.Find(".autonogrammer-settings-state").TextContent.ShouldBe(NetworkCopy.NotRun);
        cut.FindAll(".autonogrammer-settings-list-row").Count.ShouldBe(0);
        source.Runs.ShouldBe(0, "nothing is checked until the button is pressed");

        await cut.Find("button.autonogrammer-btn--primary").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll(".autonogrammer-settings-list-row").Count.ShouldBe(2)).ConfigureAwait(true);
        source.Runs.ShouldBe(1);
        var rows = cut.FindAll(".autonogrammer-settings-list-row");
        rows[0].TextContent.ShouldContain("OpenAI");
        rows[0].TextContent.ShouldContain("api.openai.com");
        rows[0].QuerySelector(".autonogrammer-chip")!.TextContent.Trim().ShouldBe(NetworkCopy.StatusLabel(EndpointStatus.Reachable));
        rows[1].QuerySelector(".autonogrammer-chip")!.TextContent.Trim().ShouldBe(NetworkCopy.StatusLabel(EndpointStatus.TimedOut));
        cut.Find("[role='status'][aria-live='polite']").TextContent.ShouldBe(NetworkCopy.Finished(1, 2));
        AssertAccessible(cut);
    }

    /// <summary>The Keyboard page lists the shortcut catalogue grouped by scope in the catalogue's scope order, each gesture in a kbd.</summary>
    [Fact]
    public async Task KeyboardPageListsShortcutsByScope()
    {
        var page = new KeyboardSettingsPage(new SettingsFixtures.KeyboardSource
        {
            Rows =
            [
                new ShortcutRow("Send now", "Ctrl+Enter", KeyboardCopy.ScopeComposer),
                new ShortcutRow("Open settings", "Ctrl+,", KeyboardCopy.ScopeApplication),
            ],
        });
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        var cut = Render<KeyboardSettingsView>(parameters => parameters.Add(p => p.Page, page));

        cut.FindAll(".autonogrammer-settings-shortcut-group").Select(static group => group.GetAttribute("aria-label")).ShouldBe([KeyboardCopy.ScopeApplication, KeyboardCopy.ScopeComposer]);
        cut.FindAll("kbd").Select(static kbd => kbd.TextContent).ShouldBe(["Ctrl+,", "Ctrl+Enter"]);
        cut.FindAll("li").Select(static item => item.QuerySelector(".autonogrammer-settings-label")!.TextContent).ShouldBe(["Open settings", "Send now"]);
    }

    /// <summary>A Rules, Skills or Commands page is a read-only table of name, scope, origin and description, with the files that could not be read listed below.</summary>
    [Fact]
    public async Task CustomizePageListsEntriesAndProblemsReadOnly()
    {
        var source = new SettingsFixtures.RulesSource
        {
            Catalog = new CustomizeCatalog(
                [new CustomizeEntry("style.mdc", CustomizeScope.Project, ".autonogrammer/rules/style.mdc", "Code style rules")],
                ["broken.mdc: the file could not be read"]),
        };
        var page = new RulesSettingsPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        var cut = Render<CustomizeSettingsView>(parameters => parameters.Add(p => p.Page, page));

        cut.Find("h3").TextContent.ShouldBe(page.Title);
        cut.FindAll("th[scope='col']").Select(static header => header.TextContent).ShouldBe(
            [CustomizeCopy.ColumnTitle, CustomizeCopy.ColumnScope, CustomizeCopy.ColumnOrigin, CustomizeCopy.ColumnDescription]);
        var cells = cut.FindAll("tbody tr")[0];
        cells.QuerySelector("th[scope='row']")!.TextContent.ShouldBe("style.mdc");
        cells.QuerySelector(".autonogrammer-chip")!.TextContent.ShouldBe("Project");
        cells.TextContent.ShouldContain(".autonogrammer/rules/style.mdc");
        cells.TextContent.ShouldContain("Code style rules");
        cut.Find(".autonogrammer-settings-problems li").TextContent.ShouldBe("broken.mdc: the file could not be read");
        cut.Find(".autonogrammer-settings-table-wrap").GetAttribute("tabindex").ShouldBe("0", "a scrolling table must be reachable by keyboard");
        cut.FindAll("input, textarea, select").Count.ShouldBe(0);
    }

    /// <summary>An empty Customize page says what to add; a failed one shows the reason with Try again.</summary>
    [Fact]
    public async Task CustomizePageShowsEmptyAndErrorStates()
    {
        var source = new SettingsFixtures.RulesSource { FailWith = "The rules folder is unreadable" };
        var page = new RulesSettingsPage(source);
        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cut = Render<CustomizeSettingsView>(parameters => parameters.Add(p => p.Page, page));
        cut.Find("[role='alert']").TextContent.ShouldBe("The rules folder is unreadable");

        await cut.Find(".autonogrammer-settings-state button").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-settings-state").TextContent.ShouldBe(page.EmptyText)).ConfigureAwait(true);
    }

    private static List<string> PressedLabels<T>(IRenderedComponent<T> cut, string groupSelector)
        where T : IComponent =>
        cut.FindAll(groupSelector + " button[aria-pressed='true']").Select(static button => button.TextContent).ToList();

    private static AngleSharp.Dom.IElement Choice<T>(IRenderedComponent<T> cut, string groupSelector, string label)
        where T : IComponent =>
        cut.FindAll(groupSelector + " button").Single(button => string.Equals(button.TextContent, label, StringComparison.Ordinal));

    private static AngleSharp.Dom.IElement SaveLine<T>(IRenderedComponent<T> cut)
        where T : IComponent =>
        cut.Find(".autonogrammer-settings-saveline");

    private static List<string> ThemeCalls(BunitJSModuleInterop module) =>
        module.Invocations.Identifiers.Contains("setTheme", StringComparer.Ordinal) ? module.Invocations["setTheme"].Select(static call => (string)call.Arguments[0]!).ToList() : [];

    /// <summary>Every control has an accessible name, every id a control points to exists, and nothing is inline.</summary>
    private static void AssertAccessible<T>(IRenderedComponent<T> cut)
        where T : IComponent
    {
        foreach (var element in cut.FindAll("button, input, select, textarea"))
        {
            var named = element.HasAttribute("aria-label") || element.TextContent.Trim().Length > 0 || cut.FindAll("label[for='" + element.Id + "']").Count > 0;
            named.ShouldBeTrue("a control has no accessible name: " + element.OuterHtml);
        }

        foreach (var element in cut.FindAll("[aria-describedby]"))
        {
            foreach (var id in element.GetAttribute("aria-describedby")!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                cut.FindAll("#" + id).Count.ShouldBe(1, "aria-describedby points to a missing id: " + id);
            }
        }

        cut.Markup.ShouldNotContain("<script", Case.Insensitive);
        cut.FindAll("[style]").Count.ShouldBe(0);
    }
}
