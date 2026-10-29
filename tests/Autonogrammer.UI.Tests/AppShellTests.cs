using Autonogrammer.UI.Components.Shell;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The app shell exposes its landmarks, puts the skip link first, and runs the off-canvas agent drawer (PX-08).</summary>
public sealed class AppShellTests : UiTestContext
{
    private const string ShellModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-shell.js";
    private const string FocusInterop = "Blazor._internal.domWrapper.focus";
    private const string MenuButton = "button.autonogrammer-topbar-menu";

    /// <summary>Matches the elements a single Tab can reach, in document order.</summary>
    private const string FocusableSelector = "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";

    /// <summary>The shell has one banner, one navigation named Agents, one main and one contentinfo, in that order, as direct children of the shell layout.</summary>
    [Fact]
    public void ShellExposesTheFourLandmarks()
    {
        var cut = RenderShell();

        var landmarks = cut.FindAll("header, nav, main, footer").Select(static element => element.TagName).ToList();
        landmarks.ShouldBe(["HEADER", "NAV", "MAIN", "FOOTER"]);
        var header = cut.Find("header");
        header.ParentElement!.ClassList.ShouldContain("autonogrammer-shell", StringComparer.Ordinal, "the banner must not sit inside the main landmark");
        cut.Find("footer").ParentElement!.ClassList.ShouldContain("autonogrammer-shell", StringComparer.Ordinal);
        var nav = cut.Find("nav");
        nav.GetAttribute("aria-label").ShouldBe("Agents");
        nav.Id.ShouldBe("autonogrammer-rail");
        nav.TextContent.ShouldContain("rail-content");
        var main = cut.Find("main");
        main.Id.ShouldBe("main");
        main.TextContent.ShouldContain("thread-content");
        main.TextContent.ShouldNotContain("rail-content");
        cut.FindAll("h1").ShouldBeEmpty("the shell draws no heading: each view renders its own h1, and the wordmark is a link home");
        cut.Find("header a.autonogrammer-wordmark").TextContent.ShouldBe("Autonogrammer");
    }

    /// <summary>The skip link is the first control a Tab reaches, it targets the main landmark by default, and a host can retarget it.</summary>
    [Fact]
    public void SkipLinkIsFirstInTheTabOrderAndTargetsMain()
    {
        var cut = RenderShell();

        var first = cut.FindAll(FocusableSelector)[0];
        first.TagName.ShouldBe("A");
        first.ClassList.ShouldContain("autonogrammer-skip-link", StringComparer.Ordinal);
        first.GetAttribute("href").ShouldBe("#main");
        first.TextContent.ShouldBe("Skip to main content");
        cut.FindAll("a.autonogrammer-skip-link").Count.ShouldBe(1);
        cut.Find("main").GetAttribute("tabindex").ShouldBe("-1", "the skip target must accept programmatic focus");

        var retargeted = Render<AppShell>(parameters => parameters
            .Add(p => p.SkipLinkHref, "#autonogrammer-conversation")
            .Add(p => p.SkipLinkText, "Skip to conversation"));
        retargeted.Find("a.autonogrammer-skip-link").GetAttribute("href").ShouldBe("#autonogrammer-conversation");
        retargeted.Find("a.autonogrammer-skip-link").TextContent.ShouldBe("Skip to conversation");
    }

    /// <summary>The drawer starts closed; the top-bar button names it, reports its state and controls the rail.</summary>
    [Fact]
    public void DrawerStartsClosedAndTheButtonReportsItsState()
    {
        var cut = RenderShell();

        var button = cut.Find(MenuButton);
        button.GetAttribute("aria-label").ShouldBe("Agents panel");
        button.GetAttribute("aria-controls").ShouldBe("autonogrammer-rail");
        button.GetAttribute("aria-expanded").ShouldBe("false");
        cut.Find(".autonogrammer-shell").GetAttribute("data-drawer").ShouldBe("closed");
        cut.FindAll(".autonogrammer-scrim").Count.ShouldBe(0);
        cut.Find("main").HasAttribute("inert").ShouldBeFalse();
    }

    /// <summary>Opening the drawer sets aria-expanded, shows the scrim, makes the main content inert and traps focus in the rail.</summary>
    [Fact]
    public void OpeningTheDrawerExpandsItAndTrapsFocus()
    {
        var module = JSInterop.SetupModule(ShellModule);
        var cut = RenderShell();

        cut.Find(MenuButton).Click();

        cut.Find(MenuButton).GetAttribute("aria-expanded").ShouldBe("true");
        cut.Find(".autonogrammer-shell").GetAttribute("data-drawer").ShouldBe("open");
        cut.FindAll(".autonogrammer-scrim").Count.ShouldBe(1);
        cut.Find("main").HasAttribute("inert").ShouldBeTrue("the page behind the drawer must not take focus");
        var trap = module.VerifyInvoke("trapFocus");
        trap.Arguments.Count.ShouldBe(2, "the trap takes the rail element and the callback reference");
        IdOf(trap.Arguments[0]).ShouldNotBeNullOrEmpty("the trap receives a real element reference of the rail");
    }

    /// <summary>Escape closes an open drawer, releases the focus trap and returns focus to the button that opened it.</summary>
    [Fact]
    public async Task EscapeClosesTheDrawerAndReturnsFocusToTheOpener()
    {
        var module = JSInterop.SetupModule(ShellModule);
        var cut = RenderShell();
        await cut.Find(MenuButton).ClickAsync().ConfigureAwait(true);
        module.VerifyNotInvoke("releaseFocus");

        await cut.Find(".autonogrammer-shell").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);

        cut.Find(MenuButton).GetAttribute("aria-expanded").ShouldBe("false");
        cut.Find(".autonogrammer-shell").GetAttribute("data-drawer").ShouldBe("closed");
        cut.FindAll(".autonogrammer-scrim").Count.ShouldBe(0);
        cut.Find("main").HasAttribute("inert").ShouldBeFalse();
        module.VerifyInvoke("releaseFocus");
        await AssertFocusWentToTheOpenerAsync(cut).ConfigureAwait(true);
    }

    /// <summary>Other keys leave the drawer open.</summary>
    [Fact]
    public void OtherKeysDoNotCloseTheDrawer()
    {
        JSInterop.SetupModule(ShellModule);
        var cut = RenderShell();
        cut.Find(MenuButton).Click();

        cut.Find(".autonogrammer-shell").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.Find(MenuButton).GetAttribute("aria-expanded").ShouldBe("true");
        JSInterop.VerifyNotInvoke(FocusInterop);
    }

    /// <summary>The scrim, the close button in the rail and the top-bar button each close the drawer and return focus to the opener.</summary>
    [Theory]
    [InlineData(".autonogrammer-scrim")]
    [InlineData("button[aria-label='Close agents panel']")]
    [InlineData(MenuButton)]
    public async Task EveryCloseControlClosesTheDrawerAndReturnsFocus(string closeSelector)
    {
        JSInterop.SetupModule(ShellModule);
        var cut = RenderShell();
        await cut.Find(MenuButton).ClickAsync().ConfigureAwait(true);

        await cut.Find(closeSelector).ClickAsync().ConfigureAwait(true);

        cut.Find(".autonogrammer-shell").GetAttribute("data-drawer").ShouldBe("closed");
        cut.Find(MenuButton).GetAttribute("aria-expanded").ShouldBe("false");
        await AssertFocusWentToTheOpenerAsync(cut).ConfigureAwait(true);
    }

    /// <summary>Growing the window past the breakpoint closes the drawer without pulling focus away from where the user is.</summary>
    [Fact]
    public async Task WideningTheViewportClosesTheDrawerWithoutMovingFocus()
    {
        var module = JSInterop.SetupModule(ShellModule);
        var cut = RenderShell();
        await cut.Find(MenuButton).ClickAsync().ConfigureAwait(true);

        await cut.InvokeAsync(() => cut.Instance.CloseFromViewportAsync()).ConfigureAwait(true);

        cut.Find(".autonogrammer-shell").GetAttribute("data-drawer").ShouldBe("closed");
        cut.Instance.IsDrawerOpen.ShouldBeFalse();
        module.VerifyInvoke("releaseFocus");
        JSInterop.VerifyNotInvoke(FocusInterop);
    }

    /// <summary>Closing a drawer that is already closed does nothing: no focus move, no trap release.</summary>
    [Fact]
    public async Task ClosingAClosedDrawerIsANoOp()
    {
        var module = JSInterop.SetupModule(ShellModule);
        var cut = RenderShell();

        await cut.InvokeAsync(() => cut.Instance.CloseDrawerAsync()).ConfigureAwait(true);

        module.VerifyNotInvoke("releaseFocus");
        JSInterop.VerifyNotInvoke(FocusInterop);
    }

    /// <summary>The top bar shows the workspace label when given one, and the source link and theme switch always.</summary>
    [Fact]
    public void TopBarShowsWorkspaceSourceLinkAndThemeSwitch()
    {
        var cut = Render<AppShell>(parameters => parameters.Add(p => p.WorkspaceLabel, "my-workspace"));

        var bar = cut.Find("header");
        bar.QuerySelector(".autonogrammer-workspace-label")!.TextContent.ShouldBe("my-workspace");
        bar.QuerySelector("a.autonogrammer-topbar-link")!.GetAttribute("href").ShouldBe(ShellInfo.SourceRepositoryUrl);
        bar.QuerySelector("[role='group'][aria-label='Theme']").ShouldNotBeNull();
        bar.QuerySelector(".autonogrammer-logo")!.GetAttribute("aria-hidden").ShouldBe("true", "the mark is decorative: the wordmark names the app");

        var without = Render<AppShell>();
        without.FindAll(".autonogrammer-workspace-label").Count.ShouldBe(0);
    }

    private static string IdOf(object? argument) => ((ElementReference)argument!).Id;

    /// <summary>
    /// bUnit cannot map an element reference to a DOM element (the markup carries no reference ids under .NET 10), so the check is done at the
    /// component boundary: the one focus request made while closing must name the same reference as <see cref="TopBar.FocusDrawerButtonAsync"/>,
    /// which is documented to focus the drawer button.
    /// </summary>
    private async Task AssertFocusWentToTheOpenerAsync(IRenderedComponent<AppShell> cut)
    {
        JSInterop.VerifyFocusAsyncInvoke();
        await cut.InvokeAsync(() => cut.FindComponent<TopBar>().Instance.FocusDrawerButtonAsync().AsTask()).ConfigureAwait(true);

        var calls = JSInterop.Invocations[FocusInterop];
        calls.Count.ShouldBe(2);
        IdOf(calls[0].Arguments[0]).ShouldNotBeNullOrEmpty();
        IdOf(calls[0].Arguments[0]).ShouldBe(IdOf(calls[1].Arguments[0]));
    }

    private IRenderedComponent<AppShell> RenderShell() => Render<AppShell>(parameters => parameters
        .Add(p => p.Rail, "<button type=\"button\" id=\"rail-button\">rail-content</button>")
        .Add(p => p.ChildContent, "<p>thread-content</p>"));
}
