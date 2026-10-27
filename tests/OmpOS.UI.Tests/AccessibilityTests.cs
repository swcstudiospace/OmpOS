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

    /// <summary>The bundled stylesheets carry the focus-visible ring, the reduced-motion rule and the 32 px target size with a 24 px floor.</summary>
    [Fact]
    public void BundledCssHasFocusAndMotionRules()
    {
        var css = string.Concat(CssSources.AllCssFiles().Select(File.ReadAllText));

        css.ShouldContain(":focus-visible");
        css.ShouldContain("prefers-reduced-motion");
        var light = CssSources.EffectiveTokens(CssSources.Theme.Light);
        light["--ompos-control-height"].ShouldBe("32px");
        light["--ompos-target-min"].ShouldBe("24px");
        light["--ompos-focus-width"].ShouldBe("2px");
    }

    /// <summary>Every text role keeps WCAG AA contrast (4.5:1) on every surface role it can sit on, in every theme.</summary>
    [Theory]
    [MemberData(nameof(TextRoleOnSurfaceCases))]
    public void TextRolesMeetContrastMinimumOnEverySurface(string themeName, string foregroundToken, string backgroundToken)
    {
        var theme = Enum.Parse<CssSources.Theme>(themeName);
        var ratio = CssSources.ContrastRatio(CssSources.Hex(theme, foregroundToken), CssSources.Hex(theme, backgroundToken));

        ratio.ShouldBeGreaterThanOrEqualTo(4.5, $"{foregroundToken} on {backgroundToken} in {theme} must meet WCAG AA for text");
    }

    /// <summary>Filled-control text meets WCAG AA for text (4.5:1) on the accent fill and on its hover fill.</summary>
    [Theory]
    [InlineData("Light", "--ompos-accent-on", "--ompos-accent")]
    [InlineData("Light", "--ompos-accent-on", "--ompos-accent-hover")]
    [InlineData("Dark", "--ompos-accent-on", "--ompos-accent")]
    [InlineData("Dark", "--ompos-accent-on", "--ompos-accent-hover")]
    [InlineData("System", "--ompos-accent-on", "--ompos-accent")]
    [InlineData("System", "--ompos-accent-on", "--ompos-accent-hover")]
    public void FilledControlTextPairsMeetContrastMinimum(string themeName, string foregroundToken, string backgroundToken)
    {
        var theme = Enum.Parse<CssSources.Theme>(themeName);
        var ratio = CssSources.ContrastRatio(CssSources.Hex(theme, foregroundToken), CssSources.Hex(theme, backgroundToken));

        ratio.ShouldBeGreaterThanOrEqualTo(4.5, $"{foregroundToken} on {backgroundToken} in {theme} must meet WCAG AA for text");
    }

    /// <summary>The focus ring and the input outline are non-text indicators: they meet 3:1 (WCAG 1.4.11) on every surface.</summary>
    [Theory]
    [MemberData(nameof(NonTextOnSurfaceCases))]
    public void FocusRingAndOutlinesMeetNonTextContrastMinimum(string themeName, string indicatorToken, string backgroundToken)
    {
        var theme = Enum.Parse<CssSources.Theme>(themeName);
        var ratio = CssSources.ContrastRatio(CssSources.Hex(theme, indicatorToken), CssSources.Hex(theme, backgroundToken));

        ratio.ShouldBeGreaterThanOrEqualTo(3.0, $"{indicatorToken} on {backgroundToken} in {theme} must meet WCAG 1.4.11");
    }

    /// <summary>A focus-visible rule draws a 2 px ring from the token the contrast tests check, not from a literal colour.</summary>
    [Fact]
    public void FocusVisibleRuleUsesTheFocusToken()
    {
        var css = File.ReadAllText(Path.Combine(CssSources.StylesDirectory, "base.css"));

        FocusVisibleRingRule().IsMatch(css).ShouldBeTrue("a :focus-visible rule must use outline with var(--ompos-focus)");
        CssSources.EffectiveTokens(CssSources.Theme.Light)["--ompos-focus-width"].ShouldBe("2px");
    }

    /// <summary>Every ratio the token comments state matches the ratio computed from the two colours, so a comment cannot go stale (phase 25.7, F-F).</summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("System")]
    public void StylesheetContrastCommentsMatchTheComputedRatios(string themeName)
    {
        var theme = Enum.Parse<CssSources.Theme>(themeName);
        var documented = 0;
        foreach (var line in CssSources.ThemeBlock(theme).Split('\n'))
        {
            var declaration = TokenDeclaration().Match(line);
            if (!declaration.Success)
            {
                continue;
            }

            foreach (Match claim in DocumentedRatio().Matches(line))
            {
                var stated = double.Parse(claim.Groups["ratio"].Value, CultureInfo.InvariantCulture);
                var computed = CssSources.ContrastRatio(declaration.Groups["color"].Value, CssSources.Hex(theme, claim.Groups["against"].Value));

                Math.Abs(computed - stated).ShouldBeLessThan(0.01, $"{declaration.Groups["token"].Value} says {stated}:1 on {claim.Groups["against"].Value} in {theme}, computed {computed:0.00}:1");
                documented++;
            }
        }

        documented.ShouldBe(10, "expected the ten documented ratios in each theme block");
    }

    /// <summary>Gets the (theme, text token, surface token) triples whose text contrast the stylesheet promises.</summary>
    public static TheoryData<string, string, string> TextRoleOnSurfaceCases()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var theme in Enum.GetValues<CssSources.Theme>())
        {
            foreach (var text in TextRoles)
            {
                foreach (var surface in Surfaces)
                {
                    data.Add(theme.ToString(), text, surface);
                }
            }
        }

        return data;
    }

    /// <summary>Gets the (theme, indicator token, surface token) triples whose non-text contrast the stylesheet promises.</summary>
    public static TheoryData<string, string, string> NonTextOnSurfaceCases()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var theme in Enum.GetValues<CssSources.Theme>())
        {
            foreach (var indicator in new[] { "--ompos-focus", "--ompos-border-strong" })
            {
                foreach (var surface in Surfaces)
                {
                    data.Add(theme.ToString(), indicator, surface);
                }
            }
        }

        return data;
    }

    private static readonly string[] TextRoles =
    [
        "--ompos-text",
        "--ompos-text-secondary",
        "--ompos-accent",
        "--ompos-success",
        "--ompos-warning",
        "--ompos-error",
    ];

    private static readonly string[] Surfaces =
    [
        "--ompos-surface-sunken",
        "--ompos-surface",
        "--ompos-surface-raised",
        "--ompos-hover",
        "--ompos-selected",
        "--ompos-surface-active",
    ];

    [GeneratedRegex("^\\s*(?<token>--ompos-[a-z-]+):\\s*(?<color>#[0-9a-fA-F]{6});", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TokenDeclaration();

    [GeneratedRegex("(?<ratio>\\d+\\.\\d+):1 on (?<against>--ompos-[a-z-]+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DocumentedRatio();

    [GeneratedRegex("focus-visible[^{]*\\{[^}]*outline:[^;}]*var\\(--ompos-focus\\)", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FocusVisibleRingRule();
}
