using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Presentation.Settings.Models;
using OmpOS.UI.Components.Settings;
using OmpOS.Web.Services.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests.Settings;

/// <summary>The Settings pages the web head registers: which pages exist, their order, what each is backed by, and that each one loads from the Demo.</summary>
public sealed class WebSettingsPagesTests : BunitContext
{
    private static readonly string[] ExpectedTitles =
    [
        "General",
        "Runtime",
        "Agents",
        "Run mode",
        "Models",
        "Tab",
        "Rules",
        "Skills",
        "Commands",
        "Hooks",
        "MCP",
        "Plugins",
        "Workspaces",
        "Remote environments",
        "Cloud agents",
        "Privacy",
        "Network",
        "Keyboard",
    ];

    private readonly DemoTestHost _host = new();
    private readonly SettingsPagesHost _pages;

    /// <summary>Initializes a new instance of the <see cref="WebSettingsPagesTests"/> class.</summary>
    public WebSettingsPagesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _pages = new SettingsPagesHost(_host, Services.GetRequiredService<IJSRuntime>());
    }

    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    /// <summary>The window holds the eighteen pages in the Cursor-like order, every section once.</summary>
    [Fact]
    public void TheWindowOffersEveryPageInTheWebOrder()
    {
        _pages.Window.Pages.Select(static page => page.Title).ShouldBe(ExpectedTitles);
        _pages.Window.Pages.Select(static page => page.Section).ShouldBe(WebSettingsOrder.Sections);
        _pages.Window.Pages.Select(static page => page.Section).Distinct().Count().ShouldBe(ExpectedTitles.Length);
        _pages.Window.SelectedSection.ShouldBe(SettingsSection.General);
    }

    /// <summary>Each page is of the kind that the dialog has a view for, and the Models page runs over the API source.</summary>
    [Fact]
    public void EveryPageIsOfAKindTheDialogRenders()
    {
        var kinds = _pages.Window.Pages.ToDictionary(static page => page.Section, static page => page.GetType());

        kinds[SettingsSection.General].ShouldBe(typeof(GeneralPage));
        kinds[SettingsSection.Runtime].ShouldBe(typeof(RuntimeValuesPage));
        kinds[SettingsSection.Agents].ShouldBe(typeof(InfoSettingsPage));
        kinds[SettingsSection.RunMode].ShouldBe(typeof(InfoSettingsPage));
        kinds[SettingsSection.Models].ShouldBe(typeof(ModelsSettingsPage));
        kinds[SettingsSection.Tab].ShouldBe(typeof(TabPage));
        kinds[SettingsSection.Rules].ShouldBe(typeof(RulesSettingsPage));
        kinds[SettingsSection.Skills].ShouldBe(typeof(SkillsSettingsPage));
        kinds[SettingsSection.Commands].ShouldBe(typeof(CommandsSettingsPage));
        kinds[SettingsSection.Hooks].ShouldBe(typeof(HooksListPage));
        kinds[SettingsSection.Mcp].ShouldBe(typeof(McpFilesPage));
        kinds[SettingsSection.Plugins].ShouldBe(typeof(PluginsPage));
        kinds[SettingsSection.Network].ShouldBe(typeof(NetworkSettingsPage));
        kinds[SettingsSection.Keyboard].ShouldBe(typeof(KeyboardSettingsPage));
        foreach (var section in new[] { SettingsSection.Workspaces, SettingsSection.Remote, SettingsSection.Cloud, SettingsSection.Privacy })
        {
            kinds[section].ShouldBe(typeof(InfoSettingsPage));
        }
    }

    /// <summary>Opening the window loads every page from the Demo and none ends in an error.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task EveryPageLoadsFromTheDemo()
    {
        await _pages.Window.LoadAllAsync(Token).ConfigureAwait(true);

        foreach (var page in _pages.Window.Pages)
        {
            page.HasError.ShouldBeFalse(page.Title);
            switch (page)
            {
                case ModelsSettingsPage models:
                    models.State.ShouldBe(ModelsPageState.Ready);
                    break;
                case CustomizeCatalogPage customize:
                    customize.State.ShouldBe(CustomizePageState.Ready, page.Title);
                    break;
                case SettingsPageBase basePage:
                    basePage.State.ShouldBe(SettingsPageState.Ready, page.Title + ": " + basePage.LoadError);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>The Runtime page shows the values of the service read-only, in four groups.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task RuntimeShowsTheServiceValuesReadOnly()
    {
        var runtime = _pages.Window.Pages.OfType<RuntimeValuesPage>().Single();

        await runtime.LoadAsync(Token).ConfigureAwait(true);

        runtime.IsDirty.ShouldBeFalse();
        runtime.Groups.Select(static group => group.Heading).ShouldBe(["Engine and stores", "Run lease", "Request limits", "Workspace"]);
        var rows = runtime.Groups.SelectMany(static group => group.Rows).ToDictionary(static row => row.Label, static row => row.Value, StringComparer.Ordinal);
        rows["Lease time"].ShouldBe("30 s");
        rows["Renew every"].ShouldBe("10 s");
        rows["Longest prompt"].ShouldBe("64,000 characters");
        rows["Workspace folder"].ShouldBe("Configured");
    }

    /// <summary>The Rules, Skills and Commands pages list the acme-shop conventions with their origins.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task RulesSkillsAndCommandsListTheWorkspaceConventions()
    {
        await _pages.Window.LoadAllAsync(Token).ConfigureAwait(true);

        var rules = _pages.Window.Pages.OfType<RulesSettingsPage>().Single();
        var skills = _pages.Window.Pages.OfType<SkillsSettingsPage>().Single();
        var commands = _pages.Window.Pages.OfType<CommandsSettingsPage>().Single();

        rules.Entries.Select(static entry => entry.Title).ShouldBe(["code-style", "AGENTS.md"]);
        skills.Entries.Select(static entry => entry.Title).ShouldBe(["review-pr", "release-notes", "explain-error"]);
        skills.Entries.Select(static entry => entry.ScopeLabel).Distinct(StringComparer.Ordinal).Count().ShouldBe(2, "project and user skills");
        commands.Entries.Count(static entry => entry.Title.Contains("test-product", StringComparison.Ordinal)).ShouldBe(1);
        commands.Entries.Count(static entry => entry.Title.Contains("changelog", StringComparison.Ordinal)).ShouldBe(1);
        skills.Problems.ShouldBeEmpty();
    }

    /// <summary>The Hooks page lists three hooks, the hook file and the problem; the MCP page lists both files and the malformed one's problem.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task HooksAndMcpListFilesAndProblemsReadOnly()
    {
        var hooks = _pages.Window.Pages.OfType<HooksListPage>().Single();
        var mcp = _pages.Window.Pages.OfType<McpFilesPage>().Single();

        await hooks.LoadAsync(Token).ConfigureAwait(true);
        await mcp.LoadAsync(Token).ConfigureAwait(true);

        hooks.Hooks.Count.ShouldBe(3);
        hooks.Sources.Single().File.ShouldBe(".cursor/hooks.json");
        hooks.Diagnostics.Count.ShouldBe(1);
        HooksListPage.CommandText(hooks.Hooks[1]).ShouldBe("npx prettier --write .");
        HooksListPage.CommandText(hooks.Hooks[0]).ShouldBe("./.cursor/hooks/audit-shell.sh");
        mcp.Files.Select(static file => file.Origin).ShouldBe([".cursor/mcp.json", ".mcp.json"]);
        mcp.Problems.Single().Origin.ShouldBe(".mcp.json");
        mcp.IsEmpty.ShouldBeFalse();
    }

    /// <summary>The honest pages state a reason, and the desktop-only ones say they are configured in the desktop app; Privacy has nothing to configure.</summary>
    [Fact]
    public void InformationPagesSayWhereTheSettingLives()
    {
        var info = _pages.Window.Pages.OfType<InfoSettingsPage>().ToDictionary(static page => page.Section);

        info.Keys.ShouldBe([SettingsSection.Agents, SettingsSection.RunMode, SettingsSection.Workspaces, SettingsSection.Remote, SettingsSection.Cloud, SettingsSection.Privacy], ignoreOrder: true);
        foreach (var (section, page) in info)
        {
            page.Facts.Count.ShouldBeGreaterThan(0, section.ToString());
            page.Description.ShouldNotBeNullOrWhiteSpace();
            page.IsDirty.ShouldBeFalse();
            if (section == SettingsSection.Privacy)
            {
                page.DesktopNotice.ShouldBeNull();
            }
            else
            {
                page.DesktopNotice.ShouldNotBeNullOrWhiteSpace(section.ToString());
            }
        }

        info[SettingsSection.Privacy].Facts.Select(static fact => fact.Text).ShouldContain(static text => text.Contains("Nothing leaves this browser tab", StringComparison.Ordinal));
    }

    /// <summary>Without the Demo, the Privacy page names the service host and does not claim the page sends nothing.</summary>
    [Fact]
    public void PrivacyNamesTheServiceHostOutsideTheDemo()
    {
        var page = WebInfoPages.Privacy(isDemo: false, "api.example.com");

        var first = page.Facts[0].Text;
        first.ShouldContain("api.example.com", Case.Sensitive);
        first.ShouldNotContain("Nothing leaves", Case.Sensitive);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pages.Dispose();
            _host.Dispose();
        }

        base.Dispose(disposing);
    }
}
