using Bunit;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.GenUi;
using OmpOS.UI.Components;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>Keyboard, labeling, live-region, and stylesheet accessibility assertions (UI-03).</summary>
internal sealed class AccessibilityTests : UiTestContext
{
    /// <summary>Enter sends the draft; Shift+Enter does not send.</summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void EnterSendsShiftEnterDoesNot(bool shift, int expectedSends)
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) => Accept(sent, text)));
        cut.Find("textarea").Input("hello");

        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Enter", ShiftKey = shift });

        sent.Count.ShouldBe(expectedSends);
    }

    /// <summary>Escape stops the stream while streaming.</summary>
    [Fact]
    public void EscapeStopsWhileStreaming()
    {
        var stops = 0;
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.IsStreaming, true)
            .Add(p => p.OnStop, () => stops++));

        cut.Find("textarea").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        stops.ShouldBe(1);
    }

    /// <summary>Every icon-only button carries a non-empty aria-label.</summary>
    [Fact]
    public void IconButtonsHaveAccessibleNames()
    {
        var session = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.Streaming)
            .Add(p => p.AutoScrollPaused, true));
        var composer = Render<Composer>();

        var iconButtons = session.FindAll("button.ompos-icon-btn")
            .Concat(composer.FindAll("button.ompos-icon-btn"))
            .ToList();
        iconButtons.Count.ShouldBeGreaterThan(0);
        foreach (var button in iconButtons)
        {
            button.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        }
    }

    /// <summary>The session view exposes a polite live region for throttled announcements.</summary>
    [Fact]
    public void LiveRegionIsPresent()
    {
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.Streaming)
            .Add(p => p.Announcement, "Assistant is responding."));

        var live = cut.Find("[aria-live=\"polite\"]");
        live.ShouldNotBeNull();
        live.TextContent.ShouldContain("Assistant is responding.");
    }

    /// <summary>The bundled stylesheet carries the focus-visible ring and the reduced-motion rule.</summary>
    [Fact]
    public void BundledCssHasFocusAndMotionRules()
    {
        var css = File.ReadAllText(FindCssPath());

        css.ShouldContain(":focus-visible");
        css.ShouldContain("prefers-reduced-motion");
        css.ShouldContain("44px");
    }

    /// <summary>The skip link is the first focusable element and targets the conversation region.</summary>
    [Theory]
    [InlineData(SessionViewState.LoadingHistory)]
    [InlineData(SessionViewState.Empty)]
    [InlineData(SessionViewState.Streaming)]
    public void SkipLinkIsFirstFocusableAndTargetsConversation(SessionViewState state)
    {
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, state)
            .Add(p => p.AutoScrollPaused, true));

        var focusable = cut.FindAll("a[href], button:not([disabled]), textarea, [tabindex]:not([tabindex='-1'])");
        focusable.Count.ShouldBeGreaterThan(0);
        var first = focusable[0];
        first.TagName.ShouldBe("A");
        first.ClassList.Contains("ompos-skip-link").ShouldBeTrue();
        first.TextContent.ShouldBe("Skip to conversation");
        var href = first.GetAttribute("href") ?? string.Empty;
        href.ShouldStartWith("#");
        cut.Find(href).ShouldNotBeNull();
    }

    /// <summary>Icon-button token pairs meet WCAG AA for text glyphs (4.5:1).</summary>
    [Theory]
    [InlineData("--ompos-fg", "--ompos-bg")]
    [InlineData("--ompos-surface-fg", "--ompos-surface")]
    public void IconButtonTokenPairsMeetContrastMinimum(string foregroundToken, string backgroundToken)
    {
        var css = File.ReadAllText(FindCssPath());
        var foreground = ParseColorToken(css, foregroundToken);
        var background = ParseColorToken(css, backgroundToken);

        var ratio = ContrastRatio(foreground, background);

        ratio.ShouldBeGreaterThanOrEqualTo(4.5, $"{foregroundToken} on {backgroundToken} must meet WCAG AA for text glyphs");
    }

    private static string ParseColorToken(string css, string token)
    {
        var declaration = css.IndexOf(token + ":", StringComparison.Ordinal);
        declaration.ShouldBeGreaterThanOrEqualTo(0, $"stylesheet must define {token}");
        var hash = css.IndexOf('#', declaration);
        hash.ShouldBeGreaterThanOrEqualTo(0, $"stylesheet must define {token} as a hex color");
        var value = css.Substring(hash, 7);
        value.Length.ShouldBe(7);
        value.Skip(1).All(static c => Uri.IsHexDigit(c)).ShouldBeTrue($"stylesheet must define {token} as a hex color");
        return value;
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var channel = (int offset) =>
        {
            var value = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        };
        return 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
    }

    private static string FindCssPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("OmpOS.slnx must be reachable by walking up from the test output");
        var path = Path.Combine(directory.FullName, "src", "OmpOS.UI", "wwwroot", "styles", "ompos.css");
        File.Exists(path).ShouldBeTrue($"bundled stylesheet must exist at {path}");
        return path;
    }
}
