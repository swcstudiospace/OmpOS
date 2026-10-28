using System.Globalization;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>
/// Static contract of the web head's stylesheets (PX-08): no Bootstrap, every design token a rule uses is defined in both themes, the
/// brand fonts ship as real woff2 files with their licences, the display face stays at 20 px and above, and no stylesheet but
/// tokens.css holds a colour literal.
/// </summary>
public sealed partial class CssContractTests
{
    /// <summary>No stylesheet, the HTML shell or the head's wwwroot refers to Bootstrap, and its files are gone.</summary>
    [Fact]
    public void NoBootstrapAnywhereInTheWebHead()
    {
        foreach (var file in CssSources.AllCssFiles())
        {
            File.ReadAllText(file).ShouldNotContain("bootstrap", Case.Insensitive, $"{Path.GetFileName(file)} must not refer to Bootstrap");
        }

        var wwwroot = Path.Combine(CssSources.RepoRoot, "apps", "OmpOS.Web", "wwwroot");
        File.ReadAllText(Path.Combine(wwwroot, "index.html")).ShouldNotContain("bootstrap", Case.Insensitive);
        Directory.Exists(Path.Combine(wwwroot, "lib", "bootstrap")).ShouldBeFalse("the Bootstrap template files must be deleted");
    }

    /// <summary>Every --ompos-* custom property a stylesheet reads with var() is declared somewhere in the shipped stylesheets.</summary>
    [Fact]
    public void EveryTokenARuleUsesIsDefined()
    {
        var sources = CssSources.AllCssFiles().ToDictionary(file => Path.GetFileName(file), file => CssSources.StripComments(File.ReadAllText(file)), StringComparer.Ordinal);
        var defined = sources.Values
            .SelectMany(css => DefinitionPattern().Matches(css).Select(static match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (file, css) in sources)
        {
            foreach (Match use in UsePattern().Matches(css))
            {
                defined.ShouldContain(use.Groups["name"].Value, StringComparer.Ordinal, $"{file} reads {use.Groups["name"].Value}, which no stylesheet defines");
            }
        }
    }

    /// <summary>Every colour, shadow and scrim token of the Light set is redefined for Dark, and the system-dark set repeats the Dark values exactly.</summary>
    [Fact]
    public void DarkAndSystemDarkRedefineEveryThemedToken()
    {
        var light = CssSources.OwnTokens(CssSources.Theme.Light);
        var dark = CssSources.OwnTokens(CssSources.Theme.Dark);
        var system = CssSources.OwnTokens(CssSources.Theme.System);

        var themed = light.Where(static pair => pair.Value.Contains('#', StringComparison.Ordinal) || pair.Value.Contains("rgb(", StringComparison.Ordinal) || pair.Key.StartsWith("--ompos-shadow", StringComparison.Ordinal))
            .Select(static pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
        themed.Count.ShouldBe(22, "19 colour roles, 2 shadows and the scrim are expected in the Light set");
        foreach (var token in themed)
        {
            dark.ContainsKey(token).ShouldBeTrue($"{token} has a Light value and needs a Dark value");
        }

        dark.Keys.Order(StringComparer.Ordinal).ShouldBe(system.Keys.Order(StringComparer.Ordinal), "the system-dark block must declare the same tokens as the dark block");
        foreach (var (token, value) in dark)
        {
            system[token].ShouldBe(value, $"{token} differs between the dark and the system-dark block");
        }
    }

    /// <summary>Every colour role of the design (research section 5) exists, with the Light and Dark values the desktop tokens use.</summary>
    [Theory]
    [InlineData("--ompos-surface-sunken", "#f0f0ee", "#141414")]
    [InlineData("--ompos-surface", "#fafaf9", "#181818")]
    [InlineData("--ompos-surface-raised", "#ffffff", "#1f1f1f")]
    [InlineData("--ompos-hover", "#ebebe8", "#262626")]
    [InlineData("--ompos-selected", "#e4e4e0", "#2d2d2d")]
    [InlineData("--ompos-surface-active", "#dcdcd7", "#333333")]
    [InlineData("--ompos-text", "#1c1c1a", "#ececec")]
    [InlineData("--ompos-text-secondary", "#55555a", "#a3a3a3")]
    [InlineData("--ompos-accent", "#6d28d9", "#a78bfa")]
    [InlineData("--ompos-accent-on", "#ffffff", "#141414")]
    [InlineData("--ompos-focus", "#5b21b6", "#c4b5fd")]
    [InlineData("--ompos-success", "#166534", "#4ade80")]
    [InlineData("--ompos-warning", "#92400e", "#fbbf24")]
    [InlineData("--ompos-error", "#b3261e", "#ff9a9a")]
    [InlineData("--ompos-border", "#e0e0dc", "#2c2c2c")]
    [InlineData("--ompos-border-strong", "#6f6f6b", "#808080")]
    [InlineData("--ompos-diff-added", "#e2f4e7", "#132a1c")]
    [InlineData("--ompos-diff-removed", "#fbe6e6", "#301719")]
    public void ColourRolesMatchTheDesktopTokens(string token, string light, string dark)
    {
        CssSources.Hex(CssSources.Theme.Light, token).ShouldBe(light);
        CssSources.Hex(CssSources.Theme.Dark, token).ShouldBe(dark);
        CssSources.Hex(CssSources.Theme.System, token).ShouldBe(dark);
    }

    /// <summary>The scale tokens carry the design's font sizes, line heights, gaps, radii and sizes.</summary>
    [Fact]
    public void ScaleTokensMatchTheDesign()
    {
        var tokens = CssSources.EffectiveTokens(CssSources.Theme.Light);

        string[] fontSizes = ["small:12px", "label:13px", "body:14px", "heading:16px", "display-sm:20px", "display:24px", "mono:12px", "terminal:13px"];
        foreach (var pair in fontSizes)
        {
            var parts = pair.Split(':');
            tokens["--ompos-font-size-" + parts[0]].ShouldBe(parts[1]);
        }

        string[] lineHeights = ["small:16px", "label:18px", "body:21px", "heading:22px", "display-sm:26px", "display:30px"];
        foreach (var pair in lineHeights)
        {
            var parts = pair.Split(':');
            tokens["--ompos-line-height-" + parts[0]].ShouldBe(parts[1]);
        }

        foreach (var gap in new[] { "2", "4", "6", "8", "12" })
        {
            tokens["--ompos-gap-" + gap].ShouldBe(gap + "px");
        }

        foreach (var radius in new[] { "4", "6", "8", "10", "12" })
        {
            tokens["--ompos-radius-" + radius].ShouldBe(radius + "px");
        }

        tokens["--ompos-radius-pill"].ShouldBe("999px");
        tokens["--ompos-control-height"].ShouldBe("32px");
        tokens["--ompos-control-height-small"].ShouldBe("28px");
        tokens["--ompos-rail-width"].ShouldBe("240px");
        tokens["--ompos-thread-max"].ShouldBe("760px");
        tokens["--ompos-titlebar-height"].ShouldBe("40px");
        tokens["--ompos-font-sans"].ShouldContain("'DM Sans', 'Helvetica Neue', Helvetica, Arial, sans-serif", Case.Sensitive);
        tokens["--ompos-font-mono"].ShouldContain("Menlo, Consolas, 'Liberation Mono', monospace", Case.Sensitive);
    }

    /// <summary>The layout breakpoints are the designed ones: rail 280 from 1200, drawer below 840, single column below 560.</summary>
    [Fact]
    public void LayoutDeclaresTheDesignedBreakpoints()
    {
        var layout = CssSources.StripComments(CssSources.ReadStyle("layout.css"));

        RailFromWidePattern().IsMatch(layout).ShouldBeTrue("rail 280 px from 1200 px");
        layout.ShouldContain("@media (max-width: 839.98px)");
        layout.ShouldContain("@media (max-width: 559.98px)");
        layout.ShouldContain("env(safe-area-inset-top");
        layout.ShouldContain("env(safe-area-inset-bottom");
    }

    /// <summary>Anybody (the display face) is only drawn at 20 px and above, in SemiBold; body, label and code text never use it.</summary>
    [Fact]
    public void DisplayFaceIsUsedOnlyAtDisplaySizes()
    {
        var tokens = CssSources.EffectiveTokens(CssSources.Theme.Light);
        var usedAnywhere = 0;
        foreach (var file in CssSources.AllCssFiles())
        {
            var css = CssSources.StripComments(File.ReadAllText(file));
            foreach (Match rule in RulePattern().Matches(css))
            {
                var selector = rule.Groups["selector"].Value.Trim();
                var body = rule.Groups["body"].Value;
                if (selector.StartsWith('@') || !DisplayFacePattern().IsMatch(body))
                {
                    continue;
                }

                usedAnywhere++;
                var size = FontSizePattern().Match(body);
                size.Success.ShouldBeTrue($"{Path.GetFileName(file)}: {selector} uses the display face and must set font-size");
                var pixels = ResolvePixels(size.Groups["value"].Value.Trim(), tokens);
                pixels.ShouldBeGreaterThanOrEqualTo(20, $"{Path.GetFileName(file)}: {selector} uses Anybody below 20 px");
                var weight = FontWeightPattern().Match(body);
                weight.Success.ShouldBeTrue($"{Path.GetFileName(file)}: {selector} uses the display face and must set font-weight");
                (weight.Groups["value"].Value.Trim() is "600" or "var(--ompos-font-weight-semibold)").ShouldBeTrue($"{Path.GetFileName(file)}: {selector} must draw Anybody in SemiBold");
                body.ShouldNotContain("font:", Case.Sensitive, $"{selector} must not use the font shorthand with the display face");
            }
        }

        usedAnywhere.ShouldBeGreaterThan(0, "the wordmark and the display roles use the display face");
    }

    /// <summary>The brand fonts are declared with swap, point at real woff2 files, and ship beside their OFL licence texts.</summary>
    [Fact]
    public void FontFacesPointAtRealWoff2FilesWithLicences()
    {
        var css = CssSources.StripComments(CssSources.ReadStyle("fonts.css"));
        var faces = FontFacePattern().Matches(css).Select(static match => match.Groups["body"].Value).ToList();
        faces.Count.ShouldBe(6, "DM Sans 400, 500, 600, Anybody 600, Space Mono 400 and 700");

        var declared = new List<string>();
        foreach (var face in faces)
        {
            face.ShouldContain("font-display: swap");
            var family = FamilyPattern().Match(face).Groups["value"].Value;
            var weight = WeightPattern().Match(face).Groups["value"].Value;
            declared.Add(family + " " + weight);

            var url = UrlPattern().Match(face).Groups["value"].Value;
            var path = Path.GetFullPath(Path.Combine(CssSources.StylesDirectory, url));
            File.Exists(path).ShouldBeTrue($"{url} must exist");
            path.ShouldStartWith(CssSources.FontsDirectory, Case.Sensitive);
            var header = new byte[4];
            using (var stream = File.OpenRead(path))
            {
                stream.ReadExactly(header);
            }

            System.Text.Encoding.ASCII.GetString(header).ShouldBe("wOF2", $"{url} must be a woff2 file");
            File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "OFL.txt")).ShouldBeTrue($"{url} must ship beside its OFL.txt");
        }

        declared.Order(StringComparer.Ordinal).ShouldBe(
            ["Anybody 600", "DM Sans 400", "DM Sans 500", "DM Sans 600", "Space Mono 400", "Space Mono 700"],
            Case.Sensitive,
            "every weight the design uses is declared once");
    }

    /// <summary>ompos.css is the single entry: it imports every other stylesheet of the shared UI styles, tokens first.</summary>
    [Fact]
    public void OmposCssImportsEveryOtherStylesheet()
    {
        var entry = CssSources.StripComments(CssSources.ReadStyle("ompos.css"));
        var imported = ImportPattern().Matches(entry).Select(static match => match.Groups["value"].Value).ToList();
        var shipped = Directory.GetFiles(CssSources.StylesDirectory, "*.css").Select(file => Path.GetFileName(file)).Where(static name => !string.Equals(name, "ompos.css", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();

        imported.Order(StringComparer.Ordinal).ShouldBe(shipped, Case.Sensitive, "every stylesheet beside ompos.css must be imported by it");
        imported[0].ShouldBe("tokens.css", "tokens must load first");
    }

    /// <summary>Only tokens.css holds colour literals: every other rule takes its colour from a token, so a theme switch reaches it.</summary>
    [Fact]
    public void ColourLiteralsLiveInTokensCssOnly()
    {
        foreach (var file in CssSources.AllCssFiles().Where(static file => !string.Equals(Path.GetFileName(file), "tokens.css", StringComparison.Ordinal)))
        {
            var css = CssSources.StripComments(File.ReadAllText(file));
            ColourLiteralPattern().IsMatch(css).ShouldBeFalse($"{Path.GetFileName(file)} holds a colour literal; use a token from tokens.css");
        }
    }

    /// <summary>The reduced-motion contract: the operating-system query and the Settings choice both zero the durations, and Full overrides the query.</summary>
    [Fact]
    public void ReducedMotionIsHonouredByQueryAndBySetting()
    {
        var css = CssSources.StripComments(CssSources.ReadStyle("base.css"));

        css.ShouldContain("@media (prefers-reduced-motion: reduce)");
        css.ShouldContain(":root:not([data-motion='full'])");
        css.ShouldContain(":root[data-motion='reduce']");
        css.ShouldContain("--ompos-duration: 0ms");
        css.ShouldContain("transition-duration: 0.01ms !important");
    }

    private static double ResolvePixels(string value, IReadOnlyDictionary<string, string> tokens)
    {
        var token = TokenReference().Match(value);
        if (token.Success)
        {
            return ResolvePixels(tokens[token.Groups["name"].Value], tokens);
        }

        var pixels = PixelValue().Match(value);
        pixels.Success.ShouldBeTrue($"cannot resolve the font size {value} to pixels");
        return double.Parse(pixels.Groups["number"].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("@media \\(min-width: 1200px\\)\\s*\\{\\s*\\.ompos-shell\\s*\\{\\s*--ompos-rail-width: 280px;", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RailFromWidePattern();

    [GeneratedRegex("(?<name>--ompos-[a-z0-9-]+)\\s*:", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DefinitionPattern();

    [GeneratedRegex("var\\(\\s*(?<name>--ompos-[a-z0-9-]+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UsePattern();

    [GeneratedRegex("(?<selector>[^{}]+)\\{(?<body>[^{}]*)\\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex RulePattern();

    [GeneratedRegex("font-family:[^;]*(--ompos-font-display|Anybody)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DisplayFacePattern();

    [GeneratedRegex("(?<![-\\w])font-size:\\s*(?<value>[^;]+);", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FontSizePattern();

    [GeneratedRegex("(?<![-\\w])font-weight:\\s*(?<value>[^;]+);", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FontWeightPattern();

    [GeneratedRegex("^var\\((?<name>--ompos-[a-z0-9-]+)\\)$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TokenReference();

    [GeneratedRegex("^(?<number>\\d+(\\.\\d+)?)px$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PixelValue();

    [GeneratedRegex("@font-face\\s*\\{(?<body>[^{}]*)\\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FontFacePattern();

    [GeneratedRegex("font-family:\\s*'(?<value>[^']+)'", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex FamilyPattern();

    [GeneratedRegex("font-weight:\\s*(?<value>\\d+)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex WeightPattern();

    [GeneratedRegex("url\\('(?<value>[^']+)'\\)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UrlPattern();

    [GeneratedRegex("@import url\\('(?<value>[^']+)'\\)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ImportPattern();

    [GeneratedRegex("#[0-9a-fA-F]{8}\\b|#[0-9a-fA-F]{6}\\b|#[0-9a-fA-F]{3}\\b", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ColourLiteralPattern();
}
