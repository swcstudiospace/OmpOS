using Autonogrammer.UI.Components.Settings;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>
/// The Settings dialog over the pages the web head registers, rendered with the Demo handler: every page shows, the Models page lists the whole
/// provider catalogue with working region and kind filters and plan cards, and the key field never holds a value and is disabled by the operator.
/// </summary>
public sealed class SettingsPagesRenderTests : BunitContext
{
    private const string Canary = "sk-render-canary-5a11e0cc93d4";
    private const string DialogModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-dialog.js";

    private readonly DemoTestHost _host = new();
    private SettingsPagesHost? _pages;

    /// <summary>Every page of the window renders under its own heading, with no load error, and the honest pages carry the desktop notice.</summary>
    [Fact]
    public void EveryPageRendersFromTheDemo()
    {
        var cut = RenderDialog();

        var titles = cut.FindAll("nav button").Select(static button => button.TextContent.Trim()).ToList();
        titles.Count.ShouldBe(18);
        foreach (var title in titles)
        {
            cut.FindAll("nav button").Single(button => string.Equals(button.TextContent.Trim(), title, StringComparison.Ordinal)).Click();

            cut.WaitForAssertion(() =>
            {
                cut.Find("nav button[aria-current='page']").TextContent.Trim().ShouldBe(title);
                cut.Find("h3").TextContent.Trim().ShouldNotBeNullOrWhiteSpace();
                cut.FindAll(".autonogrammer-settings-state--error").Count.ShouldBe(0, title + " ended in an error: " + cut.Find(".autonogrammer-settings-page").TextContent);
                cut.FindAll("[aria-busy='true']").Count.ShouldBe(0, title + " is still loading");
            });
        }
    }

    /// <summary>The pages that exist only in the desktop app say so in a notice with the words "Configure in the desktop app".</summary>
    /// <param name="title">The navigation label.</param>
    [Theory]
    [InlineData("Agents")]
    [InlineData("Run mode")]
    [InlineData("Workspaces")]
    [InlineData("Remote environments")]
    [InlineData("Cloud agents")]
    public void DesktopOnlyPagesSayConfigureInTheDesktopApp(string title)
    {
        var cut = RenderDialog();

        Open(cut, title);

        var notice = cut.Find("[data-desktop-only='true']");
        notice.TextContent.ShouldContain("Configure in the desktop app.", Case.Sensitive);
        cut.FindAll("button, input, select, textarea").Count(static element => element.ParentElement?.ClassName?.Contains("autonogrammer-settings-footer", StringComparison.Ordinal) == true).ShouldBe(0);
    }

    /// <summary>The Runtime page lists its values and offers no input.</summary>
    [Fact]
    public void RuntimeListsValuesWithoutInputs()
    {
        var cut = RenderDialog();

        Open(cut, "Runtime");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("64,000 characters", Case.Sensitive));
        cut.Find(".autonogrammer-settings-page").QuerySelectorAll("input, select, textarea").Length.ShouldBe(0);
    }

    /// <summary>The Hooks page lists the three hooks, the file and the problem; the MCP page lists the files and the malformed file's problem.</summary>
    [Fact]
    public void HooksAndMcpRenderTheirLists()
    {
        var cut = RenderDialog();

        Open(cut, "Hooks");
        cut.WaitForAssertion(() => cut.FindAll(".autonogrammer-hook-row").Count.ShouldBe(3));
        cut.Markup.ShouldContain("npx prettier --write .", Case.Sensitive);
        cut.Markup.ShouldContain(".cursor/hooks.json", Case.Sensitive);
        cut.Find(".autonogrammer-settings-problems").TextContent.ShouldContain("not executable", Case.Sensitive);

        Open(cut, "MCP");
        cut.WaitForAssertion(() => cut.Markup.ShouldContain(".cursor/mcp.json", Case.Sensitive));
        cut.Markup.ShouldContain(".mcp.json", Case.Sensitive);
        cut.Find(".autonogrammer-settings-problems").TextContent.ShouldContain("not valid JSON", Case.Sensitive);
    }

    /// <summary>The Models page lists every row of the catalogue as a listing card.</summary>
    [Fact]
    public void ModelsListsTheWholeCatalogue()
    {
        var cut = RenderDialog();

        Open(cut, "Models");

        cut.WaitForAssertion(() => cut.FindAll("[data-listing]").Count.ShouldBe(Autonogrammer.Harness.Providers.CatalogueRows.All.Count));
        cut.FindAll("[data-listing]").Select(static card => card.GetAttribute("data-region")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ShouldBe(["China", "Europe", "Global", "Local", "UnitedStates"]);
    }

    /// <summary>The region filter keeps only that region: Europe shows only European cards.</summary>
    [Fact]
    public void TheRegionFilterNarrowsTheDirectoryToEurope()
    {
        var cut = RenderDialog();
        Open(cut, "Models");
        cut.WaitForAssertion(() => cut.FindAll("[data-listing]").Count.ShouldBeGreaterThan(100));

        ChooseOption(cut, "#autonogrammer-directory-region", "Europe");

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-listing]");
            cards.Count.ShouldBe(Autonogrammer.Harness.Providers.CatalogueRows.All.Count(static row => row.Region == Autonogrammer.Harness.Providers.CatalogueRegion.Eu));
            cards.ShouldAllBe(static card => string.Equals(card.GetAttribute("data-region"), "Europe", StringComparison.Ordinal));
        });
        cut.FindAll("[data-listing='mistral']").Count.ShouldBe(1);
    }

    /// <summary>Region China and kind Coding plans narrow to the CN coding plans; each plan card shows its rule text and the tick box.</summary>
    [Fact]
    public void ChinaCodingPlansShowTheirRuleTextAndTheTickBox()
    {
        var cut = RenderDialog();
        Open(cut, "Models");
        cut.WaitForAssertion(() => cut.FindAll("[data-listing]").Count.ShouldBeGreaterThan(100));

        ChooseOption(cut, "#autonogrammer-directory-region", "China");
        ChooseOption(cut, "#autonogrammer-directory-kind", "Coding plans");

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-listing]");
            cards.Count.ShouldBe(Autonogrammer.Harness.Providers.CatalogueRows.All.Count(static row => row.Region == Autonogrammer.Harness.Providers.CatalogueRegion.Cn && row.Kind == Autonogrammer.Harness.Providers.CatalogueKind.CodingPlan));
            cards.ShouldAllBe(static card => string.Equals(card.GetAttribute("data-region"), "China", StringComparison.Ordinal) && string.Equals(card.GetAttribute("data-kind"), "CodingPlan", StringComparison.Ordinal));
            cards.ShouldAllBe(static card => card.QuerySelector(".autonogrammer-provider-rule") != null && card.TextContent.Contains("Vendor rule:", StringComparison.Ordinal));
        });

        var card = cut.Find("[data-listing='kimi-code']");
        card.QuerySelectorAll("button").Single(static button => string.Equals(button.TextContent.Trim(), "Set up", StringComparison.Ordinal)).Click();
        var tick = cut.Find("[data-listing='kimi-code'] input[type='checkbox']");
        tick.GetAttribute("aria-label").ShouldStartWith("I have read the vendor's rule text for ", Case.Sensitive);
        cut.Find("[data-listing='kimi-code']").TextContent.ShouldContain("I have read the vendor's rule text", Case.Sensitive);
    }

    /// <summary>The search box narrows the directory to the providers that match.</summary>
    [Fact]
    public void TheDirectorySearchNarrowsTheList()
    {
        var cut = RenderDialog();
        Open(cut, "Models");
        cut.WaitForAssertion(() => cut.FindAll("[data-listing]").Count.ShouldBeGreaterThan(100));

        cut.Find("#autonogrammer-directory-search").Input("moonshot");

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-listing]");
            cards.Count.ShouldBeGreaterThan(0);
            cards.Count.ShouldBeLessThan(10);
        });
    }

    /// <summary>The key field is write-only and disabled: it carries no value, shows the operator's sentence, sends nothing, and its Add action is off.</summary>
    [Fact]
    public void TheKeyFieldIsDisabledByTheOperatorAndNeverHoldsAValue()
    {
        var cut = RenderDialog();
        Open(cut, "Models");
        cut.WaitForAssertion(() => cut.FindAll("[data-listing]").Count.ShouldBeGreaterThan(100));
        cut.Find(".autonogrammer-settings-banner[data-key-writes='disabled']").TextContent.ShouldContain("Key entry is disabled by the operator in this Demo", Case.Sensitive);

        cut.FindAll("[data-listing='openai'] button").Single(static button => string.Equals(button.TextContent.Trim(), "Set up", StringComparison.Ordinal)).Click();

        var key = cut.Find("[data-listing='openai'] input[type='password']");
        key.HasAttribute("disabled").ShouldBeTrue();
        key.HasAttribute("value").ShouldBeFalse();
        key.GetAttribute("autocomplete").ShouldBe("off");
        cut.Find("[data-listing='openai']").TextContent.ShouldContain("disabled by the operator in this Demo", Case.Sensitive);
        cut.FindAll("[data-listing='openai'] button").Single(static button => string.Equals(button.TextContent.Trim(), "Add provider", StringComparison.Ordinal)).HasAttribute("disabled").ShouldBeTrue();
        cut.Markup.ShouldNotContain(Canary, Case.Sensitive);
        _host.Recorder.Requests.ShouldNotContain(static request => request.Contains("credential", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pages?.Dispose();
            _host.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void Open(IRenderedComponent<SettingsDialog> cut, string title) =>
        cut.FindAll("nav button").Single(button => string.Equals(button.TextContent.Trim(), title, StringComparison.Ordinal)).Click();

    private static void ChooseOption(IRenderedComponent<SettingsDialog> cut, string selector, string textStart)
    {
        var select = cut.Find(selector);
        var option = select.QuerySelectorAll("option").First(candidate => candidate.TextContent.StartsWith(textStart, StringComparison.Ordinal));
        select.Change(option.GetAttribute("value"));
    }

    private IRenderedComponent<SettingsDialog> RenderDialog()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule(DialogModule);
        module.Setup<int>("trapFocus", _ => true).SetResult(1);
        _pages = new SettingsPagesHost(_host, Services.GetRequiredService<IJSRuntime>());
        var window = _pages.Window;
        return Render<SettingsDialog>(parameters => parameters.Add(p => p.Settings, window).Add(p => p.IsOpen, true));
    }
}
