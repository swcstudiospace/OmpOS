using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests;

/// <summary>
/// Text contrast for every foreground and background token pair in the one token dictionary (XP-01, XP-08, D-20-07;
/// criterion 6). The tests parse <c>Themes/OmpOSTokens.axaml</c> as XML, so no Avalonia runtime is involved.
/// </summary>
public sealed class ThemeContrastTests
{
    private const double MinimumTextContrast = 4.5;
    private const string ColorKeyPrefix = "OmpOS.Color.";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] ForegroundTokens = { "OmpOS.Color.Text", "OmpOS.Color.TextMuted", "OmpOS.Color.Accent", "OmpOS.Color.Error" };
    private static readonly string[] BackgroundTokens = { "OmpOS.Color.Background", "OmpOS.Color.Surface", "OmpOS.Color.SurfaceActive" };
    private static readonly string[] ContractColourTokens =
    {
        "OmpOS.Color.Accent",
        "OmpOS.Color.Background",
        "OmpOS.Color.Error",
        "OmpOS.Color.Surface",
        "OmpOS.Color.SurfaceActive",
        "OmpOS.Color.Text",
        "OmpOS.Color.TextMuted",
    };

    /// <summary>The 4 foreground by 3 background pairs, enumerated exhaustively.</summary>
    public static TheoryData<string, string> Pairs
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var foreground in ForegroundTokens)
            {
                foreach (var background in BackgroundTokens)
                {
                    data.Add(foreground, background);
                }
            }

            return data;
        }
    }

    /// <summary>Each foreground and background pair reaches at least 4.5:1 with WCAG 2.x relative luminance.</summary>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void EveryTextPairMeetsTheContrastMinimum(string foreground, string background)
    {
        var colors = ReadColorTokens();

        var ratio = ContrastRatio(colors[foreground], colors[background]);

        ratio.ShouldBeGreaterThanOrEqualTo(MinimumTextContrast, $"{foreground} on {background} is {ratio:0.00}:1");
    }

    /// <summary>The pair set covers exactly the twelve pairs of the UI-SPEC contrast contract.</summary>
    [Fact]
    public void ThePairSetIsTheTwelvePairsOfTheContract()
    {
        (ForegroundTokens.Length * BackgroundTokens.Length).ShouldBe(12);
    }

    /// <summary>Every foreground and background token the pairs name is defined in the dictionary.</summary>
    [Fact]
    public void EveryPairTokenIsDefinedInTheDictionary()
    {
        var defined = ReadColorTokens();

        foreach (var token in ForegroundTokens.Concat(BackgroundTokens))
        {
            defined.ContainsKey(token).ShouldBeTrue($"{token} must be defined in OmpOSTokens.axaml");
        }
    }

    /// <summary>The dictionary defines exactly the seven colour tokens of the contract, so a new token cannot skip the test.</summary>
    [Fact]
    public void TheDictionaryDefinesExactlyTheContractColourTokens()
    {
        ReadColorTokens().Keys.OrderBy(static key => key, StringComparer.Ordinal).ShouldBe(ContractColourTokens);
    }

    /// <summary>The colour, type and spacing tokens live in one ResourceDictionary document.</summary>
    [Fact]
    public void TheTokensAreOneResourceDictionaryHoldingColourTypeAndSpacing()
    {
        var document = LoadTokenDocument();
        var keys = DefinedKeys(document).ToList();

        document.Root.ShouldNotBeNull();
        document.Root!.Name.LocalName.ShouldBe("ResourceDictionary");
        keys.Any(static key => key.StartsWith("OmpOS.Color.", StringComparison.Ordinal)).ShouldBeTrue();
        keys.Any(static key => key.StartsWith("OmpOS.Font.", StringComparison.Ordinal)).ShouldBeTrue();
        keys.Any(static key => key.StartsWith("OmpOS.Space.", StringComparison.Ordinal)).ShouldBeTrue();
    }

    private static XDocument LoadTokenDocument()
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), "apps", "OmpOS.Native", "Themes", "OmpOSTokens.axaml");
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

    private static Dictionary<string, string> ReadColorTokens()
    {
        var key = XName.Get("Key", XamlNamespace);

        return LoadTokenDocument().Descendants()
            .Where(element => ((string?)element.Attribute(key))?.StartsWith(ColorKeyPrefix, StringComparison.Ordinal) == true)
            .ToDictionary(
                element => (string)element.Attribute(key)!,
                element => NormalizeHex(element.Value.Trim()),
                StringComparer.Ordinal);
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
}
