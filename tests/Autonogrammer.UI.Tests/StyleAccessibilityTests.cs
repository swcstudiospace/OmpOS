using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// Static accessibility contract of a11y.css and of the colour pairs the stylesheets draw (WCAG 2.2 AA): the forced-colors, high-contrast, reduced-motion
/// and reduced-transparency blocks exist, the forced-colors block names system colours only, and every foreground and background pair of a rule, in
/// every theme and under the high-contrast overrides, holds 4.5:1 for text and 3:1 for the borders and the focus ring.
/// </summary>
public sealed partial class StyleAccessibilityTests
{
    private const string ForcedColorsQuery = "@media (forced-colors: active)";
    private const string ContrastQuery = "@media (prefers-contrast: more)";

    private static readonly string[] SystemColours = ["Canvas", "CanvasText", "LinkText", "ButtonText", "ButtonFace", "Highlight", "HighlightText", "GrayText"];

    private static readonly string[] TextRoles =
    [
        "--autonogrammer-text",
        "--autonogrammer-text-secondary",
        "--autonogrammer-accent",
        "--autonogrammer-success",
        "--autonogrammer-warning",
        "--autonogrammer-error",
    ];

    private static readonly string[] Surfaces =
    [
        "--autonogrammer-surface-sunken",
        "--autonogrammer-surface",
        "--autonogrammer-surface-raised",
        "--autonogrammer-hover",
        "--autonogrammer-selected",
        "--autonogrammer-surface-active",
        "--autonogrammer-tint",
        "--autonogrammer-tint-strong",
        "--autonogrammer-diff-added",
        "--autonogrammer-diff-removed",
    ];

    private static readonly string[] Indicators =
    [
        "--autonogrammer-focus",
        "--autonogrammer-border",
        "--autonogrammer-border-strong",
        "--autonogrammer-tint-border",
    ];

    /// <summary>Gets the (theme, high-contrast override) pairs the colour tests run for.</summary>
    /// <returns>One case per theme with and without the high-contrast overrides.</returns>
    public static TheoryData<string, bool> ThemeVariants()
    {
        var data = new TheoryData<string, bool>();
        foreach (var theme in Enum.GetValues<CssSources.Theme>())
        {
            data.Add(theme.ToString(), false);
            data.Add(theme.ToString(), true);
        }

        return data;
    }

    /// <summary>a11y.css declares the four preference blocks, the scroll padding for the sticky bars, the 24 px target floor and the 320 px reflow rules.</summary>
    [Fact]
    public void TheAccessibilityStylesheetDeclaresEveryPreferenceBlock()
    {
        var css = A11yCss();

        css.ShouldContain(ForcedColorsQuery);
        css.ShouldContain(ContrastQuery);
        css.ShouldContain("@media (prefers-reduced-motion: reduce)");
        css.ShouldContain("@media (prefers-reduced-transparency: reduce)");
        css.ShouldContain("scroll-padding-block-start");
        css.ShouldContain("scroll-padding-block-end");
        css.ShouldContain("scroll-margin-block");
        css.ShouldContain("min-block-size: var(--autonogrammer-target-min)");
        css.ShouldContain("@media (max-width: 359.98px)");
    }

    /// <summary>The forced-colors block holds no colour literal and no token colour: every colour it sets is a system colour, so the user's palette decides.</summary>
    [Fact]
    public void TheForcedColorsBlockNamesSystemColoursOnly()
    {
        var block = Block(A11yCss(), ForcedColorsQuery);

        HexLiteral().IsMatch(block).ShouldBeFalse("the forced-colors block must not hold a hex colour");
        ColourFunction().IsMatch(block).ShouldBeFalse("the forced-colors block must not hold rgb(), hsl(), color-mix() or another colour function");
        block.ShouldContain(":focus-visible");

        var checkedDeclarations = 0;
        foreach (Match rule in RulePattern().Matches(block))
        {
            foreach (Match declaration in ColourDeclaration().Matches(rule.Groups["body"].Value))
            {
                var value = LengthTokenReference().Replace(declaration.Groups["value"].Value, string.Empty).Trim();
                value.ShouldNotContain("var(", Case.Sensitive, $"{declaration.Groups["property"].Value} in {rule.Groups["selector"].Value.Trim()} must not take a token colour");
                (SystemColours.Any(colour => value.Contains(colour, StringComparison.Ordinal)) || value is "0" or "none" or "transparent")
                    .ShouldBeTrue($"{declaration.Groups["property"].Value}: {value} in {rule.Groups["selector"].Value.Trim()} must name a system colour");
                checkedDeclarations++;
            }
        }

        checkedDeclarations.ShouldBeGreaterThan(40, "the parser must see the colour declarations of the block");
    }

    /// <summary>A rule that opts out of forced colours names its colours as system colours, so the opt-out never draws author colours.</summary>
    [Fact]
    public void ForcedColorAdjustNoneNamesSystemColours()
    {
        foreach (var file in CssSources.AllCssFiles())
        {
            var css = CssSources.StripComments(File.ReadAllText(file));
            foreach (Match rule in RulePattern().Matches(css))
            {
                if (!rule.Groups["body"].Value.Contains("forced-color-adjust: none", StringComparison.Ordinal))
                {
                    continue;
                }

                SystemColours.Any(colour => rule.Groups["body"].Value.Contains(colour, StringComparison.Ordinal))
                    .ShouldBeTrue($"{Path.GetFileName(file)}: {rule.Groups["selector"].Value.Trim()} sets forced-color-adjust: none and must name its colours as system colours");
            }
        }
    }

    /// <summary>The high-contrast block overrides tokens by reference to other tokens, so it holds no colour of its own and both themes follow it.</summary>
    [Fact]
    public void TheHighContrastOverridesPointAtTokensAndNeverAtTranslucentColours()
    {
        var overrides = ContrastOverrides();

        overrides.Keys.ShouldContain("--autonogrammer-border", StringComparer.Ordinal);
        overrides.Keys.ShouldContain("--autonogrammer-text-secondary", StringComparer.Ordinal);
        overrides.Keys.ShouldContain("--autonogrammer-tint-border", StringComparer.Ordinal);
        foreach (var (name, value) in overrides)
        {
            HexLiteral().IsMatch(value).ShouldBeFalse($"{name} must reference a token, not a literal");
            value.ShouldNotContain("rgb", Case.Insensitive, $"{name} must not be translucent");
            value.ShouldNotContain("%", Case.Sensitive, $"{name} must not be translucent");
        }
    }

    /// <summary>Every text role keeps 4.5:1 on every surface, and the focus ring and the borders keep 3:1, in every theme with and without the high-contrast overrides.</summary>
    /// <param name="themeName">The theme name.</param>
    /// <param name="highContrast">Whether the prefers-contrast: more overrides apply.</param>
    [Theory]
    [MemberData(nameof(ThemeVariants))]
    public void TokenRolesHoldTheirRatiosInEveryThemeVariant(string themeName, bool highContrast)
    {
        var tokens = Tokens(Enum.Parse<CssSources.Theme>(themeName), highContrast);

        foreach (var text in TextRoles)
        {
            foreach (var surface in Surfaces)
            {
                Ratio(tokens, text, surface).ShouldBeGreaterThanOrEqualTo(4.5, $"{text} on {surface} in {themeName} (high contrast: {highContrast}) must hold 4.5:1");
            }
        }

        string[] indicators = highContrast ? Indicators : ["--autonogrammer-focus", "--autonogrammer-border-strong"];
        foreach (var indicator in indicators)
        {
            foreach (var surface in Surfaces.Where(static surface => !surface.Contains("diff", StringComparison.Ordinal)))
            {
                Ratio(tokens, indicator, surface).ShouldBeGreaterThanOrEqualTo(3.0, $"{indicator} on {surface} in {themeName} (high contrast: {highContrast}) must hold 3:1");
            }
        }

        if (highContrast)
        {
            foreach (var name in new[] { "--autonogrammer-border", "--autonogrammer-text-secondary", "--autonogrammer-scrim", "--autonogrammer-diff-added", "--autonogrammer-diff-removed" })
            {
                HexOnly().IsMatch(Resolve(tokens, name)).ShouldBeTrue($"{name} must resolve to an opaque #rrggbb colour under high contrast");
            }
        }
    }

    /// <summary>Every rule that sets both a token foreground and a token background holds 4.5:1 in every theme, with and without the high-contrast overrides.</summary>
    /// <param name="themeName">The theme name.</param>
    /// <param name="highContrast">Whether the prefers-contrast: more overrides apply.</param>
    [Theory]
    [MemberData(nameof(ThemeVariants))]
    public void EveryForegroundAndBackgroundPairOfARuleHoldsContrast(string themeName, bool highContrast)
    {
        var tokens = Tokens(Enum.Parse<CssSources.Theme>(themeName), highContrast);
        var pairs = 0;

        foreach (var file in CssSources.AllCssFiles().Where(static file => !string.Equals(Path.GetFileName(file), "tokens.css", StringComparison.Ordinal)))
        {
            var css = CssSources.StripComments(File.ReadAllText(file));
            foreach (Match rule in RulePattern().Matches(css))
            {
                var foreground = ForegroundToken().Match(rule.Groups["body"].Value);
                var background = BackgroundToken().Match(rule.Groups["body"].Value);
                if (!foreground.Success || !background.Success)
                {
                    continue;
                }

                var text = foreground.Groups["name"].Value;
                var surface = background.Groups["name"].Value;
                if (!tokens.ContainsKey(text) || !tokens.ContainsKey(surface) || !HexOnly().IsMatch(Resolve(tokens, text)) || !HexOnly().IsMatch(Resolve(tokens, surface)))
                {
                    continue;
                }

                pairs++;
                Ratio(tokens, text, surface).ShouldBeGreaterThanOrEqualTo(
                    4.5,
                    $"{Path.GetFileName(file)}: {rule.Groups["selector"].Value.Trim()} draws {text} on {surface} in {themeName} (high contrast: {highContrast})");
            }
        }

        pairs.ShouldBeGreaterThan(20, "the parser must see the foreground and background pairs of the stylesheets");
    }

    /// <summary>The entry stylesheet imports a11y.css last, so its rules outrank the component rules of equal specificity without !important.</summary>
    [Fact]
    public void TheAccessibilityStylesheetLoadsLast()
    {
        var entry = CssSources.StripComments(CssSources.ReadStyle("autonogrammer.css"));
        var imported = ImportPattern().Matches(entry).Select(static match => match.Groups["value"].Value).ToList();

        imported[^1].ShouldBe("a11y.css");
    }

    private static string A11yCss() => CssSources.StripComments(CssSources.ReadStyle("a11y.css"));

    private static string Block(string css, string header)
    {
        var start = css.IndexOf(header, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"a11y.css must contain {header}");
        var open = css.IndexOf('{', start);
        var depth = 0;
        for (var index = open; index < css.Length; index++)
        {
            if (css[index] == '{')
            {
                depth++;
            }
            else if (css[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return css.Substring(open + 1, index - open - 1);
                }
            }
        }

        throw new InvalidOperationException($"The block of {header} is not closed.");
    }

    private static Dictionary<string, string> ContrastOverrides()
    {
        var block = Block(A11yCss(), ContrastQuery);
        var rule = RulePattern().Matches(block).First(static match => match.Groups["selector"].Value.Contains(":root:root", StringComparison.Ordinal));
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match declaration in TokenDeclaration().Matches(rule.Groups["body"].Value))
        {
            overrides[declaration.Groups["name"].Value] = declaration.Groups["value"].Value.Trim();
        }

        return overrides;
    }

    private static Dictionary<string, string> Tokens(CssSources.Theme theme, bool highContrast)
    {
        var tokens = new Dictionary<string, string>(CssSources.EffectiveTokens(theme), StringComparer.Ordinal);
        if (highContrast)
        {
            foreach (var (name, value) in ContrastOverrides())
            {
                tokens[name] = value;
            }
        }

        return tokens;
    }

    private static string Resolve(IReadOnlyDictionary<string, string> tokens, string name)
    {
        var value = tokens[name];
        for (var depth = 0; depth < 8; depth++)
        {
            var reference = TokenReference().Match(value);
            if (!reference.Success)
            {
                break;
            }

            value = tokens[reference.Groups["name"].Value];
        }

        return value;
    }

    private static double Ratio(IReadOnlyDictionary<string, string> tokens, string foreground, string background) =>
        CssSources.ContrastRatio(Resolve(tokens, foreground), Resolve(tokens, background));

    [GeneratedRegex("@import\\s+url\\('(?<value>[^']+)'\\)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ImportPattern();

    [GeneratedRegex("(?<selector>[^{}]+)\\{(?<body>[^{}]*)\\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RulePattern();

    [GeneratedRegex("(?<name>--autonogrammer-[a-z0-9-]+)\\s*:\\s*(?<value>[^;]+);", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TokenDeclaration();

    [GeneratedRegex("^var\\((?<name>--autonogrammer-[a-z0-9-]+)\\)$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TokenReference();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HexOnly();

    [GeneratedRegex("#[0-9a-fA-F]{8}\\b|#[0-9a-fA-F]{6}\\b|#[0-9a-fA-F]{3}\\b", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HexLiteral();

    [GeneratedRegex("\\b(rgba?|hsla?|hwb|lab|lch|oklab|oklch|color-mix|color)\\(", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ColourFunction();

    [GeneratedRegex("(?<![-\\w])(?<property>color|background|background-color|border|border-top|border-right|border-bottom|border-left|border-inline-start|border-inline-end|border-color|border-inline-start-color|outline|outline-color|accent-color|text-decoration-color)\\s*:\\s*(?<value>[^;]+);", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ColourDeclaration();

    [GeneratedRegex("var\\(--autonogrammer-(focus-width|focus-offset)\\)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LengthTokenReference();

    [GeneratedRegex("(?<![-\\w])color:\\s*var\\((?<name>--autonogrammer-[a-z0-9-]+)\\)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ForegroundToken();

    [GeneratedRegex("(?<![-\\w])background(?:-color)?:\\s*var\\((?<name>--autonogrammer-[a-z0-9-]+)\\)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex BackgroundToken();
}
