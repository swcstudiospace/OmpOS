using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.GenUi;
using OmpOS.Presentation.State;
using OmpOS.UI.Components;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>Keyboard, labeling, live-region, and stylesheet accessibility assertions (UI-03).</summary>
public sealed partial class AccessibilityTests : UiTestContext
{
    /// <summary>Enter sends the draft; Shift+Enter does not send.</summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void EnterSendsShiftEnterDoesNot(bool shift, int expectedSends)
    {
        var sent = new List<string>();
        var cut = Render<Composer>(parameters => parameters
            .Add(p => p.OnSend, (string text) => AcceptAsync(sent, text)));
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

    /// <summary>Filled-control text tokens meet WCAG AA for text (4.5:1) on the fill they sit on.</summary>
    [Theory]
    [InlineData("--ompos-primary-contrast", "--ompos-primary")]
    [InlineData("--ompos-error-contrast", "--ompos-error")]
    public void FilledControlTextPairsMeetContrastMinimum(string foregroundToken, string backgroundToken)
    {
        var css = File.ReadAllText(FindCssPath());

        var ratio = ContrastRatio(ParseColorToken(css, foregroundToken), ParseColorToken(css, backgroundToken));

        ratio.ShouldBeGreaterThanOrEqualTo(4.5, $"{foregroundToken} on {backgroundToken} must meet WCAG AA for text");
    }

    /// <summary>The focus ring is a non-text indicator: it meets 3:1 (WCAG 1.4.11) on both surfaces the session draws on.</summary>
    [Theory]
    [InlineData("--ompos-focus-ring", "--ompos-bg")]
    [InlineData("--ompos-focus-ring", "--ompos-surface")]
    public void FocusRingMeetsNonTextContrastMinimum(string ringToken, string backgroundToken)
    {
        var css = File.ReadAllText(FindCssPath());

        var ratio = ContrastRatio(ParseColorToken(css, ringToken), ParseColorToken(css, backgroundToken));

        ratio.ShouldBeGreaterThanOrEqualTo(3.0, $"{ringToken} on {backgroundToken} must meet WCAG 1.4.11 for a focus indicator");
    }

    /// <summary>A focus-visible rule draws the ring from the token the contrast tests check, not from a literal colour.</summary>
    [Fact]
    public void FocusVisibleRuleUsesTheFocusRingToken()
    {
        var css = File.ReadAllText(FindCssPath());

        FocusVisibleRingRule().IsMatch(css).ShouldBeTrue("a :focus-visible rule must use outline with var(--ompos-focus-ring)");
    }

    /// <summary>Every ratio the stylesheet comments state matches the ratio computed from the two colours, so a comment cannot go stale (phase 25.7, F-F).</summary>
    [Fact]
    public void StylesheetContrastCommentsMatchTheComputedRatios()
    {
        var css = File.ReadAllText(FindCssPath());
        var documented = 0;
        foreach (var line in css.Split('\n'))
        {
            var declaration = TokenDeclaration().Match(line);
            if (!declaration.Success)
            {
                continue;
            }

            foreach (Match claim in DocumentedRatio().Matches(line))
            {
                var stated = double.Parse(claim.Groups["ratio"].Value, CultureInfo.InvariantCulture);
                var computed = ContrastRatio(declaration.Groups["color"].Value, ParseColorToken(css, claim.Groups["against"].Value));

                Math.Abs(computed - stated).ShouldBeLessThan(0.01, $"{declaration.Groups["token"].Value} says {stated}:1 on {claim.Groups["against"].Value}, computed {computed:0.00}:1");
                documented++;
            }
        }

        documented.ShouldBeGreaterThanOrEqualTo(6, "expected the six documented ratios in the token block");
    }

    [GeneratedRegex("^\\s*(?<token>--ompos-[a-z-]+):\\s*(?<color>#[0-9a-fA-F]{6});", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TokenDeclaration();

    [GeneratedRegex("(?<ratio>\\d+\\.\\d+):1 on (?<against>--ompos-[a-z-]+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DocumentedRatio();

    [GeneratedRegex("focus-visible[^{]*\\{[^}]*outline:[^;}]*var\\(--ompos-focus-ring\\)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FocusVisibleRingRule();

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
