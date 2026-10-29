using Autonogrammer.UI.Components;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The icon-only send control is named by its label alone, and the skip link is the first element a Tab reaches in the session shell.</summary>
public sealed class SendControlAndSkipLinkTests : BunitContext
{
    /// <summary>Matches the elements a single Tab can reach, in document order.</summary>
    private const string FocusableSelector = "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";

    /// <summary>The send control holds no text, so its accessible name is exactly its aria-label and its arrow icon is hidden from assistive technology.</summary>
    [Fact]
    public void SendControlHasNoTextAndIsNamedByItsLabel()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string _) => Task.FromResult(true)));

        var send = cut.Find("button[aria-label='Send message']");
        send.TextContent.Trim().ShouldBe(string.Empty, customMessage: "an icon-only control must not expose text that could join its accessible name");
        send.GetAttribute("aria-label").ShouldBe("Send message");
        var icon = send.QuerySelector("svg");
        icon.ShouldNotBeNull();
        icon!.GetAttribute("aria-hidden").ShouldBe("true");
    }

    /// <summary>In the rendered session shell, the skip link is the first element a single Tab reaches, ahead of the sidebar and the New button.</summary>
    [Fact]
    public void SkipLinkIsTheFirstFocusableElementInTheShell()
    {
        using var host = new DemoTestHost();
        var cut = RenderShellWithSession(host);

        var focusable = cut.FindAll(FocusableSelector);
        focusable.Count.ShouldBeGreaterThan(1);
        var first = focusable[0];
        first.TagName.ShouldBe("A");
        first.ClassList.Contains("autonogrammer-skip-link").ShouldBeTrue();
        cut.FindAll("a.autonogrammer-skip-link").Count.ShouldBe(1);
    }

    /// <summary>The skip link targets the conversation region, which exists in the shell and can take programmatic focus.</summary>
    [Fact]
    public void SkipLinkTargetExistsAndAcceptsProgrammaticFocus()
    {
        using var host = new DemoTestHost();
        var cut = RenderShellWithSession(host);

        var href = cut.Find("a.autonogrammer-skip-link").GetAttribute("href") ?? string.Empty;
        href.ShouldStartWith("#");
        var target = cut.Find(href);
        target.GetAttribute("tabindex").ShouldBe("-1");
    }

    /// <summary>Renders the Web page over the Demo workbench and waits until the shell and the composer are on the page.</summary>
    /// <param name="host">The Demo host the page reads.</param>
    /// <returns>The rendered shell.</returns>
    private IRenderedComponent<Autonogrammer.Web.Pages.Index> RenderShellWithSession(DemoTestHost host)
    {
        DemoPageSetup.Register(Services, host);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<Autonogrammer.Web.Pages.Index>();
        cut.WaitForAssertion(() => cut.Find("textarea").ShouldNotBeNull(), TimeSpan.FromSeconds(10));
        return cut;
    }
}
