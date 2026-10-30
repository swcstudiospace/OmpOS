using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// Contrast for the colour roles in the one token dictionary, in both theme variants (XP-01, XP-08, D-20-07;
/// criterion 6). The tests parse <c>Themes/AutonogrammerTokens.axaml</c> as XML, so no Avalonia runtime is involved.
/// </summary>
/// <remarks>
/// Each colour role is classified by the uses it has in <c>RoleUses</c>. Text pairs are the product of the text
/// foregrounds and the backgrounds; non-text pairs are the product of the non-text foregrounds and the backgrounds.
/// A pair leaves a product only through a named exclusion that carries its reason. A role that the dictionary defines
/// but that has no use fails <c>EachVariantDefinesExactlyTheClassifiedRoles</c>, so a new role or background cannot
/// reach the dictionary without being checked.
/// </remarks>
public sealed class ThemeContrastTests
{
    private const double MinimumTextContrast = 4.5;
    private const double MinimumNonTextContrast = 3.0;
    private const double MinimumDecorativeContrast = 1.15;
    private const string ColorKeyPrefix = "Autonogrammer.Color.";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private const string TextPrimary = "Autonogrammer.Color.TextPrimary";
    private const string TextSecondary = "Autonogrammer.Color.TextSecondary";
    private const string StatusError = "Autonogrammer.Color.StatusError";
    private const string StatusSuccess = "Autonogrammer.Color.StatusSuccess";
    private const string StatusWarning = "Autonogrammer.Color.StatusWarning";
    private const string AccentPurpleOn = "Autonogrammer.Color.AccentPurpleOn";
    private const string SurfaceSunken = "Autonogrammer.Color.SurfaceSunken";
    private const string Surface = "Autonogrammer.Color.Surface";
    private const string SurfaceRaised = "Autonogrammer.Color.SurfaceRaised";
    private const string SurfaceActive = "Autonogrammer.Color.SurfaceActive";
    private const string Hover = "Autonogrammer.Color.Hover";
    private const string Selected = "Autonogrammer.Color.Selected";
    private const string DiffAdded = "Autonogrammer.Color.DiffAdded";
    private const string DiffRemoved = "Autonogrammer.Color.DiffRemoved";
    private const string Border = "Autonogrammer.Color.Border";
    private const string BorderStrong = "Autonogrammer.Color.BorderStrong";
    private const string AccentPurple = "Autonogrammer.Color.AccentPurple";
    private const string FocusRing = "Autonogrammer.Color.FocusRing";

    private const string OnAccentFill = "text on the accent fill must use AccentPurpleOn, so no other text role is drawn over AccentPurple";
    private const string AccentPurpleOnlyOnAccent = "AccentPurpleOn is the text colour for the accent fill and is drawn only over AccentPurple";

    private static readonly string[] Variants = { "Light", "Dark" };

    /// <summary>The uses a colour role has in the contrast checks.</summary>
    [Flags]
    private enum RoleUse
    {
        /// <summary>No use. The classification test rejects a role that carries it.</summary>
        None = 0,

        /// <summary>A text foreground, drawn over a background at 4.5:1.</summary>
        Text = 1,

        /// <summary>A background that text or a non-text mark is drawn over.</summary>
        Background = 2,

        /// <summary>A non-text mark (border, icon or focus indicator), drawn over a background at 3:1.</summary>
        NonText = 4,

        /// <summary>
        /// A decorative hairline (divider, card outline). WCAG 1.4.11 asks nothing of it, so it enters no product; a separate
        /// test only keeps it visible.
        /// </summary>
        Decorative = 8,
    }

    /// <summary>
    /// Every colour role the dictionary defines, with its uses. <c>TextSecondary</c> is also a border colour, and
    /// <c>AccentPurple</c> is also an accent fill, a border colour and the colour of links and accent words.
    /// <c>StatusError</c> is also the border of an invalid field. <c>BorderStrong</c> outlines inputs, switches and checkboxes.
    /// <c>FocusRing</c> is reserved for focus indicators.
    /// </summary>
    private static readonly Dictionary<string, RoleUse> RoleUses = new(StringComparer.Ordinal)
    {
        [TextPrimary] = RoleUse.Text,
        [TextSecondary] = RoleUse.Text | RoleUse.NonText,
        [StatusError] = RoleUse.Text | RoleUse.NonText,
        [StatusSuccess] = RoleUse.Text,
        [StatusWarning] = RoleUse.Text,
        [AccentPurpleOn] = RoleUse.Text,
        [SurfaceSunken] = RoleUse.Background,
        [Surface] = RoleUse.Background,
        [SurfaceRaised] = RoleUse.Background,
        [Hover] = RoleUse.Background,
        [Selected] = RoleUse.Background,
        [SurfaceActive] = RoleUse.Background,
        [DiffAdded] = RoleUse.Background,
        [DiffRemoved] = RoleUse.Background,
        [AccentPurple] = RoleUse.Text | RoleUse.Background | RoleUse.NonText,
        [FocusRing] = RoleUse.NonText,
        [BorderStrong] = RoleUse.NonText,
        [Border] = RoleUse.Decorative,
    };

    private static readonly string[] TextForegrounds = RolesWith(RoleUse.Text);
    private static readonly string[] Backgrounds = RolesWith(RoleUse.Background);
    private static readonly string[] NonTextForegrounds = RolesWith(RoleUse.NonText);

    /// <summary>A pair left out of a product by design, with the reason it is left out.</summary>
    private sealed record Exclusion(string Foreground, string Background, string Reason);

    /// <summary>Text pairs that are not checked, each with its reason.</summary>
    private static readonly Exclusion[] TextExclusions =
    {
        new(TextPrimary, AccentPurple, OnAccentFill),
        new(TextSecondary, AccentPurple, OnAccentFill),
        new(StatusError, AccentPurple, OnAccentFill),
        new(StatusSuccess, AccentPurple, OnAccentFill),
        new(StatusWarning, AccentPurple, OnAccentFill),
        new(AccentPurpleOn, Surface, AccentPurpleOnlyOnAccent),
        new(AccentPurpleOn, SurfaceRaised, AccentPurpleOnlyOnAccent),
        new(AccentPurpleOn, SurfaceActive, AccentPurpleOnlyOnAccent),
        new(AccentPurpleOn, SurfaceSunken, AccentPurpleOnlyOnAccent),
        new(AccentPurpleOn, Hover, AccentPurpleOnlyOnAccent),
        new(AccentPurpleOn, Selected, AccentPurpleOnlyOnAccent),
        new(AccentPurpleOn, DiffAdded, AccentPurpleOnlyOnAccent),
        new(AccentPurpleOn, DiffRemoved, AccentPurpleOnlyOnAccent),
        new(AccentPurple, AccentPurple, "a role has no contrast against itself"),
    };

    /// <summary>Non-text pairs that are not checked, each with its reason.</summary>
    private static readonly Exclusion[] NonTextExclusions =
    {
        new(FocusRing, AccentPurple, "the focus ring is never drawn on the accent fill"),
        new(TextSecondary, AccentPurple, "no view draws a TextSecondary border or icon over the accent fill"),
        new(AccentPurple, AccentPurple, "a role has no contrast against itself"),
        new(BorderStrong, AccentPurple, "no view draws a BorderStrong outline over the accent fill"),
        new(StatusError, AccentPurple, "no field border is drawn over the accent fill"),
    };

    /// <summary>Text pairs (4.5:1): every text foreground over every background, less the reasoned exclusions.</summary>
    private static readonly Pair[] TextPairs = Product(TextForegrounds, Backgrounds, TextExclusions);

    /// <summary>Non-text pairs (3:1): every non-text foreground over every background, less the reasoned exclusions.</summary>
    private static readonly Pair[] NonTextPairs = Product(NonTextForegrounds, Backgrounds, NonTextExclusions);

    /// <summary>Each text pair in each variant.</summary>
    public static TheoryData<string, string, string> TextPairCases => PairCases(TextPairs);

    /// <summary>Each non-text pair in each variant.</summary>
    public static TheoryData<string, string, string> NonTextPairCases => PairCases(NonTextPairs);

    /// <summary>Each text pair reaches at least 4.5:1 with WCAG 2.x relative luminance, in both variants.</summary>
    [Theory]
    [MemberData(nameof(TextPairCases))]
    public void EveryTextPairMeetsTheTextMinimum(string variant, string foreground, string background)
    {
        var colours = ReadVariant(variant);

        var ratio = ContrastRatio(colours[foreground], colours[background]);

        ratio.ShouldBeGreaterThanOrEqualTo(MinimumTextContrast, $"{variant}: {foreground} on {background} is {ratio:0.00}:1");
    }

    /// <summary>Each non-text pair reaches at least 3:1 with WCAG 2.x relative luminance, in both variants.</summary>
    [Theory]
    [MemberData(nameof(NonTextPairCases))]
    public void EveryNonTextPairMeetsTheNonTextMinimum(string variant, string foreground, string background)
    {
        var colours = ReadVariant(variant);

        var ratio = ContrastRatio(colours[foreground], colours[background]);

        ratio.ShouldBeGreaterThanOrEqualTo(MinimumNonTextContrast, $"{variant}: {foreground} on {background} is {ratio:0.00}:1");
    }

    /// <summary>
    /// Each variant defines exactly the classified roles, so a role added to the dictionary without a use fails here,
    /// and a use added without a dictionary entry fails here too.
    /// </summary>
    [Fact]
    public void EachVariantDefinesExactlyTheClassifiedRoles()
    {
        var classified = RoleUses.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();

        foreach (var variant in Variants)
        {
            var defined = ReadVariant(variant).Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
            bool equal = defined.SequenceEqual(classified, StringComparer.Ordinal);
            equal.ShouldBeTrue($"the {variant} variant must define exactly the classified roles");
        }
    }

    /// <summary>Every classified role carries at least one use, so none is classified as nothing.</summary>
    [Fact]
    public void EveryClassifiedRoleHasAUse()
    {
        foreach (var (role, use) in RoleUses)
        {
            (use != RoleUse.None).ShouldBeTrue($"{role} must carry at least one use");
        }
    }

    /// <summary>The exclusions name pairs of their product, are unique, and each states a reason.</summary>
    [Fact]
    public void EveryExclusionNamesAPairOfItsProductWithAReason()
    {
        AssertExclusions(TextExclusions, TextForegrounds, Backgrounds, "text");
        AssertExclusions(NonTextExclusions, NonTextForegrounds, Backgrounds, "non-text");
    }

    /// <summary>
    /// The decorative <c>Border</c> hairline enters no WCAG product, but a divider must not vanish: it stays at least 1.15:1
    /// against the three base surfaces it is drawn on, in both variants.
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void TheDecorativeBorderStaysVisibleOnTheBaseSurfaces(string variant)
    {
        var colours = ReadVariant(variant);

        foreach (var surface in new[] { Surface, SurfaceSunken, SurfaceRaised })
        {
            var ratio = ContrastRatio(colours[Border], colours[surface]);

            ratio.ShouldBeGreaterThanOrEqualTo(MinimumDecorativeContrast, $"{variant}: Border on {surface} is {ratio:0.00}:1");
        }
    }

    /// <summary>The text pairs cover error and secondary text on the interaction fill, which the view markup draws.</summary>
    [Fact]
    public void TheTextPairsCoverErrorAndSecondaryTextOnSurfaceActive()
    {
        TextPairs.ShouldContain(new Pair(StatusError, SurfaceActive));
        TextPairs.ShouldContain(new Pair(TextSecondary, SurfaceActive));
    }

    /// <summary>
    /// The generated pair lists contain every pair the Agent Panel status chips, glyph strokes, selected-row bar and primary buttons rely on
    /// (46-UI-SPEC, Contrast contract), so each is already measured by the theory tests above in both variants.
    /// </summary>
    [Fact]
    public void ThePanelStatusPairsAreCovered()
    {
        string[] surfaces = [Surface, SurfaceRaised, SurfaceActive];
        string[] statusText = [TextPrimary, TextSecondary, StatusSuccess, StatusWarning, StatusError];

        foreach (var background in surfaces)
        {
            foreach (var foreground in statusText)
            {
                TextPairs.ShouldContain(new Pair(foreground, background), $"{foreground} on {background} is drawn as status text");
            }

            NonTextPairs.ShouldContain(new Pair(AccentPurple, background), $"AccentPurple on {background} is the focus ring, selected bar and glyph stroke");
            NonTextPairs.ShouldContain(new Pair(TextSecondary, background), $"TextSecondary on {background} is the chip border");
        }

        TextPairs.ShouldContain(new Pair(AccentPurpleOn, AccentPurple), "the primary button label is AccentPurpleOn on AccentPurple");
    }

    /// <summary>Every colour role sits inside a Light or Dark theme variant, however deep, so no colour is read outside the variant that selects it.</summary>
    [Fact]
    public void EveryColourRoleIsDefinedInsideAThemeVariant()
    {
        var key = XName.Get("Key", XamlNamespace);

        foreach (var role in LoadTokenDocument().Descendants().Where(IsColourKey))
        {
            var dictionary = role.Ancestors().First(static ancestor => string.Equals(ancestor.Name.LocalName, "ResourceDictionary", StringComparison.Ordinal));
            var variant = (string?)dictionary.Attribute(key);

            (variant is "Light" or "Dark").ShouldBeTrue($"{(string?)role.Attribute(key)} must be defined inside a Light or Dark theme variant");
        }
    }

    /// <summary>The font families resolve to the bundled faces, each followed by its fallbacks.</summary>
    [Fact]
    public void TheFontTokensNameTheBundledFaces()
    {
        ReadTokenText("Autonogrammer.Font.Family").ShouldStartWith("avares://Autonogrammer.Native.Views/Assets/Fonts/DMSans#DM Sans");
        ReadTokenText("Autonogrammer.Font.Display").ShouldStartWith("avares://Autonogrammer.Native.Views/Assets/Fonts/Anybody#Anybody");
        ReadTokenText("Autonogrammer.Font.Mono").ShouldStartWith("avares://Autonogrammer.Native.Views/Assets/Fonts/SpaceMono#Space Mono");
    }

    /// <summary>Every colour role in both variants is a six or eight digit hex colour.</summary>
    [Fact]
    public void EveryColourRoleIsAHexColour()
    {
        foreach (var variant in Variants)
        {
            ReadVariant(variant).Values.ShouldAllBe(static value => value.Length == 7 && value[0] == '#');
        }
    }

    /// <summary>The colour, type, radius and spacing tokens live in one ResourceDictionary document.</summary>
    [Fact]
    public void TheTokensAreOneResourceDictionaryHoldingColourTypeRadiusAndSpacing()
    {
        var document = LoadTokenDocument();
        var keys = DefinedKeys(document).ToList();

        document.Root.ShouldNotBeNull();
        document.Root!.Name.LocalName.ShouldBe("ResourceDictionary");
        keys.Any(static key => key.StartsWith("Autonogrammer.Color.", StringComparison.Ordinal)).ShouldBeTrue();
        keys.Any(static key => key.StartsWith("Autonogrammer.Font.", StringComparison.Ordinal)).ShouldBeTrue();
        keys.Any(static key => key.StartsWith("Autonogrammer.Radius.", StringComparison.Ordinal)).ShouldBeTrue();
        keys.Any(static key => key.StartsWith("Autonogrammer.Space.", StringComparison.Ordinal)).ShouldBeTrue();
    }

    private static string[] RolesWith(RoleUse use)
    {
        return RoleUses
            .Where(entry => entry.Value.HasFlag(use))
            .Select(static entry => entry.Key)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
    }

    private static Pair[] Product(string[] foregrounds, string[] backgrounds, Exclusion[] exclusions)
    {
        return foregrounds
            .SelectMany(foreground => backgrounds.Select(background => new Pair(foreground, background)))
            .Where(pair => !exclusions.Any(exclusion => string.Equals(exclusion.Foreground, pair.Foreground, StringComparison.Ordinal) && string.Equals(exclusion.Background, pair.Background, StringComparison.Ordinal)))
            .ToArray();
    }

    private static void AssertExclusions(Exclusion[] exclusions, string[] foregrounds, string[] backgrounds, string kind)
    {
        exclusions.Select(static exclusion => (exclusion.Foreground, exclusion.Background)).Distinct().Count()
            .ShouldBe(exclusions.Length, $"each {kind} exclusion must name a distinct pair");

        foreach (var exclusion in exclusions)
        {
            foregrounds.ShouldContain(exclusion.Foreground, StringComparer.Ordinal, $"{kind} exclusion {exclusion.Foreground} on {exclusion.Background} names a role with no {kind} foreground use");
            backgrounds.ShouldContain(exclusion.Background, StringComparer.Ordinal, $"{kind} exclusion {exclusion.Foreground} on {exclusion.Background} names a role with no background use");
            exclusion.Reason.ShouldNotBeNullOrWhiteSpace($"{kind} exclusion {exclusion.Foreground} on {exclusion.Background} must state its reason");
        }
    }

    private static TheoryData<string, string, string> PairCases(IEnumerable<Pair> pairs)
    {
        var data = new TheoryData<string, string, string>();
        foreach (var variant in Variants)
        {
            foreach (var pair in pairs)
            {
                data.Add(variant, pair.Foreground, pair.Background);
            }
        }

        return data;
    }

    private static XDocument LoadTokenDocument()
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Themes", "AutonogrammerTokens.axaml");
        File.Exists(path).ShouldBeTrue($"the token dictionary must exist at {path}");
        return XDocument.Load(path);
    }

    private static IEnumerable<string> DefinedKeys(XDocument document)
    {
        var key = XName.Get("Key", XamlNamespace);
        return document.Descendants()
            .Select(element => (string?)element.Attribute(key))
            .Where(static name => name is not null)
            .Select(static name => name!);
    }

    private static bool IsColourKey(XElement element)
    {
        return ((string?)element.Attribute(XName.Get("Key", XamlNamespace)))?.StartsWith(ColorKeyPrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>Reads the colour roles of one theme variant (<c>Light</c> or <c>Dark</c>) as <c>#RRGGBB</c> values.</summary>
    private static Dictionary<string, string> ReadVariant(string variant)
    {
        var key = XName.Get("Key", XamlNamespace);

        var sections = LoadTokenDocument().Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ResourceDictionary", StringComparison.Ordinal) && string.Equals((string?)element.Attribute(key), variant, StringComparison.Ordinal))
            .ToList();
        sections.Count.ShouldBe(1, $"the dictionary must define exactly one {variant} theme variant");

        return sections[0].Elements()
            .Where(static element => string.Equals(element.Name.LocalName, "SolidColorBrush", StringComparison.Ordinal) && IsColourKey(element))
            .ToDictionary(
                element => (string)element.Attribute(key)!,
                element => NormalizeHex(element.Value.Trim()),
                StringComparer.Ordinal);
    }

    private static string ReadTokenText(string tokenKey)
    {
        var key = XName.Get("Key", XamlNamespace);
        var element = LoadTokenDocument().Descendants().SingleOrDefault(candidate => string.Equals((string?)candidate.Attribute(key), tokenKey, StringComparison.Ordinal));
        element.ShouldNotBeNull($"{tokenKey} must be defined in AutonogrammerTokens.axaml");
        return element!.Value.Trim();
    }

    /// <summary>Accepts <c>#RRGGBB</c> and <c>#AARRGGBB</c> and returns <c>#RRGGBB</c>. The alpha byte is dropped.</summary>
    private static string NormalizeHex(string value)
    {
        value.StartsWith('#').ShouldBeTrue($"colour token value '{value}' must start with #");
        var digits = value[1..];
        if (digits.Length == 8)
        {
            digits = digits[2..];
        }

        digits.Length.ShouldBe(6, $"colour token value '{value}' must be #RRGGBB or #AARRGGBB");
        digits.All(Uri.IsHexDigit).ShouldBeTrue($"colour token value '{value}' must be hexadecimal");
        return "#" + digits.ToUpperInvariant();
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>WCAG 2.x relative luminance: each sRGB channel is linearised before the weighted sum.</summary>
    private static double RelativeLuminance(string hex)
    {
        static double Linearise(string hexPair)
        {
            var channel = Convert.ToInt32(hexPair, 16) / 255.0;
            return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linearise(hex.Substring(1, 2)))
            + (0.7152 * Linearise(hex.Substring(3, 2)))
            + (0.0722 * Linearise(hex.Substring(5, 2)));
    }

    /// <summary>A foreground and a background colour role, by token name.</summary>
    private readonly record struct Pair(string Foreground, string Background);
}
