using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The web page is the shell: one banner, the Agents navigation, the main region with the composer, the footer with the source offer, and the top bar controls. The page mounts the workbench, here over the Demo.</summary>
public sealed class ShellLayoutTests : BunitContext
{
    /// <summary>The page has the banner, the Agents navigation, the main region and the composer.</summary>
    [Fact]
    public void PageIsTheShellWithRailThreadAndFooter()
    {
        using var host = new DemoTestHost();
        var cut = RenderPage(host);

        cut.FindAll("header").Count(static header => header.Closest("main") is null).ShouldBe(1, "one banner: a header inside main (the thread header) is not a banner landmark");
        cut.Find("nav[aria-label='Agents']").ShouldNotBeNull();
        var main = cut.Find("main#main");
        main.QuerySelector("textarea[aria-label='Message']").ShouldNotBeNull();
        cut.Find(".autonogrammer-shell").ShouldNotBeNull();
        cut.Find("footer").ShouldNotBeNull();
    }

    /// <summary>The footer carries the source offer and the licence.</summary>
    [Fact]
    public void FooterCarriesTheSourceOffer()
    {
        using var host = new DemoTestHost();
        var cut = RenderPage(host);

        var footer = cut.Find("footer");
        footer.QuerySelector("a[href='https://github.com/swcstudiospace/OmpOS']").ShouldNotBeNull();
        footer.TextContent.ShouldContain("AGPL-3.0-or-later");
        footer.QuerySelector(".autonogrammer-footer-version")!.TextContent.ShouldStartWith("Autonogrammer ");
    }

    /// <summary>The top bar holds the theme switch, the workspace label and the source link.</summary>
    [Fact]
    public void TopBarHoldsTheThemeSwitchWorkspaceAndSourceLink()
    {
        using var host = new DemoTestHost();
        var cut = RenderPage(host);

        var header = cut.Find("header");
        header.QuerySelectorAll(".autonogrammer-theme-switch button").Length.ShouldBe(3);
        header.QuerySelector(".autonogrammer-workspace-label").ShouldNotBeNull();
        header.QuerySelector("a.autonogrammer-topbar-link[href='https://github.com/swcstudiospace/OmpOS']").ShouldNotBeNull();
    }

    private IRenderedComponent<Autonogrammer.Web.Pages.Index> RenderPage(DemoTestHost host)
    {
        DemoPageSetup.Register(Services, host);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<Autonogrammer.Web.Pages.Index>();
        cut.WaitForAssertion(() => cut.Find("main#main textarea[aria-label='Message']").ShouldNotBeNull(), TimeSpan.FromSeconds(10));
        return cut;
    }
}
