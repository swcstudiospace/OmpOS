using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests;

/// <summary>
/// The typography rules of the Greptile faces (AL-23) hold in the markup and the code: Anybody, the display face (<c>OmpOS.Font.Display</c>), is only used at
/// 20 px and above and in SemiBold or heavier (the one bundled weight), and no view names a font family by literal: every face comes from a token, and
/// only the token dictionary declares the families.
/// </summary>
public sealed partial class TypographyMarkupTests
{
    private const string DisplayToken = "OmpOS.Font.Display";
    private const double MinimumDisplaySize = 20;
    private const int MinimumDisplayWeight = 600;
    private const int ContextLines = 6;

    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Every <c>FontFamily</c> setter or attribute that uses the display token also sets a size of at least 20 and SemiBold or heavier.</summary>
    [Fact]
    public async Task EveryUseOfTheDisplayFaceIsAtLeast20PxAndSemiBold()
    {
        var tokens = await ReadTokensAsync().ConfigureAwait(true);
        var violations = new List<string>();
        var uses = 0;
        foreach (var file in MarkupFiles())
        {
            var found = DisplayFaceViolations(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true), tokens, out var count);
            uses += count;
            violations.AddRange(found.Select(violation => $"{Path.GetRelativePath(ShellHarness.RepoRoot(), file)}: {violation}"));
        }

        uses.ShouldBeGreaterThan(0, "the markup uses the display face, so the scan has something to check");
        violations.ShouldBeEmpty();
    }

    /// <summary>The code that draws the display face (<c>OmpOS.Font.Display</c> or the family name) sets a size of 20 or more and a SemiBold weight beside it.</summary>
    [Fact]
    public async Task CodeThatNamesTheDisplayFaceSetsALargeSemiBoldSize()
    {
        var violations = new List<string>();
        foreach (var file in CodeFiles())
        {
            var lines = await File.ReadAllLinesAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains(DisplayToken, StringComparison.Ordinal) && !lines[index].Contains("Anybody", StringComparison.Ordinal))
                {
                    continue;
                }

                var window = string.Join('\n', lines.Skip(Math.Max(0, index - ContextLines)).Take((2 * ContextLines) + 1));
                var sized = window.Contains("Font.Size.Display", StringComparison.Ordinal) || LargeLiteralSize().IsMatch(window);
                var heavy = window.Contains("Weight.Semibold", StringComparison.Ordinal) || window.Contains("FontWeight.SemiBold", StringComparison.Ordinal) || window.Contains("FontWeight.Bold", StringComparison.Ordinal);
                if (!sized || !heavy)
                {
                    violations.Add($"{Path.GetRelativePath(ShellHarness.RepoRoot(), file)}:{index + 1} uses the display face without a size of 20 or more and a SemiBold weight");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>No view, style or head names a font family by literal: every <c>FontFamily</c> is a token reference, and only the token dictionary holds the family list.</summary>
    [Fact]
    public async Task NoViewOrHeadNamesAFontFamilyByLiteral()
    {
        var violations = new List<string>();
        foreach (var file in MarkupFiles())
        {
            var found = LiteralFamilyViolations(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true));
            violations.AddRange(found.Select(violation => $"{Path.GetRelativePath(ShellHarness.RepoRoot(), file)}: {violation}"));
        }

        foreach (var file in CodeFiles())
        {
            var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
            if (LiteralFamilyInCode().IsMatch(text))
            {
                violations.Add($"{Path.GetRelativePath(ShellHarness.RepoRoot(), file)}: constructs a FontFamily from a literal name");
            }
        }

        violations.ShouldBeEmpty();
    }

    /// <summary>The scanner sees a display face that is too small, too light or without a size, and a literal family: it would fail the rules above.</summary>
    [Fact]
    public void TheScannerReportsWhatItShould()
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OmpOS.Font.Size.Body"] = "14",
            ["OmpOS.Font.Size.DisplaySm"] = "20",
            ["OmpOS.Font.Weight.Medium"] = "Medium",
            ["OmpOS.Font.Weight.Semibold"] = "SemiBold",
        };
        const string Header = """<Styles xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">""";

        DisplayFaceViolations(Header + """
            <Style Selector="TextBlock.ok">
              <Setter Property="FontFamily" Value="{DynamicResource OmpOS.Font.Display}" />
              <Setter Property="FontSize" Value="{DynamicResource OmpOS.Font.Size.DisplaySm}" />
              <Setter Property="FontWeight" Value="{DynamicResource OmpOS.Font.Weight.Semibold}" />
            </Style></Styles>
            """, tokens, out var okUses).ShouldBeEmpty();
        okUses.ShouldBe(1);

        DisplayFaceViolations(Header + """
            <Style Selector="TextBlock.small">
              <Setter Property="FontFamily" Value="{DynamicResource OmpOS.Font.Display}" />
              <Setter Property="FontSize" Value="{DynamicResource OmpOS.Font.Size.Body}" />
              <Setter Property="FontWeight" Value="{DynamicResource OmpOS.Font.Weight.Semibold}" />
            </Style></Styles>
            """, tokens, out _).Count.ShouldBe(1);

        DisplayFaceViolations(Header + """
            <Style Selector="TextBlock.light">
              <Setter Property="FontFamily" Value="{DynamicResource OmpOS.Font.Display}" />
              <Setter Property="FontSize" Value="24" />
              <Setter Property="FontWeight" Value="{DynamicResource OmpOS.Font.Weight.Medium}" />
            </Style></Styles>
            """, tokens, out _).Count.ShouldBe(1);

        DisplayFaceViolations(Header + """<TextBlock FontFamily="{DynamicResource OmpOS.Font.Display}" Text="x" /></Styles>""", tokens, out _).Count.ShouldBe(2);
        DisplayFaceViolations(Header + """<TextBlock FontFamily="{DynamicResource OmpOS.Font.Display}" FontSize="20" FontWeight="Bold" Text="x" /></Styles>""", tokens, out _).ShouldBeEmpty();

        LiteralFamilyViolations(Header + """<TextBlock FontFamily="Arial" /><Style Selector="x"><Setter Property="FontFamily" Value="Helvetica" /></Style></Styles>""").Count.ShouldBe(2);
        LiteralFamilyViolations(Header + """<TextBlock FontFamily="{DynamicResource OmpOS.Font.Mono}" /></Styles>""").ShouldBeEmpty();
    }

    private static List<string> DisplayFaceViolations(string markup, IReadOnlyDictionary<string, string> tokens, out int uses)
    {
        var violations = new List<string>();
        var count = 0;
        var root = XDocument.Parse(markup, LoadOptions.SetLineInfo).Root!;

        // Style setters: the family, size and weight setters of one style are compared with each other.
        foreach (var style in root.Descendants(Avalonia + "Style"))
        {
            var setters = style.Elements(Avalonia + "Setter").ToDictionary(static setter => (string?)setter.Attribute("Property") ?? string.Empty, static setter => (string?)setter.Attribute("Value") ?? string.Empty, StringComparer.Ordinal);
            if (setters.TryGetValue("FontFamily", out var family) && family.Contains(DisplayToken, StringComparison.Ordinal))
            {
                count++;
                Check(violations, $"style '{(string?)style.Attribute("Selector")}'", setters.GetValueOrDefault("FontSize"), setters.GetValueOrDefault("FontWeight"), tokens);
            }
        }

        // Elements: a family attribute on an element names its own size and weight.
        foreach (var element in root.Descendants().Where(static element => element.Name != Avalonia + "Setter"))
        {
            var family = (string?)element.Attribute("FontFamily");
            if (family is not null && family.Contains(DisplayToken, StringComparison.Ordinal))
            {
                count++;
                Check(violations, $"<{element.Name.LocalName}> at line {((System.Xml.IXmlLineInfo)element).LineNumber}", (string?)element.Attribute("FontSize"), (string?)element.Attribute("FontWeight"), tokens);
            }
        }

        uses = count;
        return violations;
    }

    private static void Check(List<string> violations, string where, string? size, string? weight, IReadOnlyDictionary<string, string> tokens)
    {
        if (size is null || !TryResolveSize(size, tokens, out var points))
        {
            violations.Add($"{where} uses the display face without a resolvable FontSize");
        }
        else if (points < MinimumDisplaySize)
        {
            violations.Add($"{where} uses the display face at {points.ToString(CultureInfo.InvariantCulture)} px, below {MinimumDisplaySize.ToString(CultureInfo.InvariantCulture)}");
        }

        if (weight is null || !TryResolveWeight(weight, tokens, out var value))
        {
            violations.Add($"{where} uses the display face without a resolvable FontWeight");
        }
        else if (value < MinimumDisplayWeight)
        {
            violations.Add($"{where} uses the display face at weight {value.ToString(CultureInfo.InvariantCulture)}, below SemiBold");
        }
    }

    private static List<string> LiteralFamilyViolations(string markup)
    {
        var violations = new List<string>();
        var root = XDocument.Parse(markup, LoadOptions.SetLineInfo).Root!;
        foreach (var setter in root.Descendants(Avalonia + "Setter").Where(static setter => string.Equals((string?)setter.Attribute("Property"), "FontFamily", StringComparison.Ordinal)))
        {
            AddIfLiteral(violations, (string?)setter.Attribute("Value"), ((System.Xml.IXmlLineInfo)setter).LineNumber);
        }

        foreach (var element in root.Descendants().Where(static element => element.Attribute("FontFamily") is not null))
        {
            AddIfLiteral(violations, (string?)element.Attribute("FontFamily"), ((System.Xml.IXmlLineInfo)element).LineNumber);
        }

        return violations;
    }

    private static void AddIfLiteral(List<string> violations, string? value, int line)
    {
        if (value is not null && !value.StartsWith("{DynamicResource OmpOS.Font.", StringComparison.Ordinal) && !value.StartsWith("{StaticResource OmpOS.Font.", StringComparison.Ordinal))
        {
            violations.Add($"line {line} names the font family '{value}' by literal instead of an OmpOS.Font token");
        }
    }

    private static bool TryResolveSize(string value, IReadOnlyDictionary<string, string> tokens, out double points) =>
        double.TryParse(Resolve(value, tokens), NumberStyles.Float, CultureInfo.InvariantCulture, out points);

    private static bool TryResolveWeight(string value, IReadOnlyDictionary<string, string> tokens, out int weight)
    {
        var resolved = Resolve(value, tokens);
        weight = resolved switch
        {
            "Thin" => 100,
            "ExtraLight" => 200,
            "Light" => 300,
            "Normal" or "Regular" => 400,
            "Medium" => 500,
            "SemiBold" => 600,
            "Bold" => 700,
            "ExtraBold" => 800,
            "Black" => 900,
            _ => 0,
        };
        return weight > 0 || int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out weight);
    }

    private static string Resolve(string value, IReadOnlyDictionary<string, string> tokens)
    {
        var match = TokenReference().Match(value);
        return match.Success && tokens.TryGetValue(match.Groups["key"].Value, out var resolved) ? resolved : value.Trim();
    }

    private static async Task<Dictionary<string, string>> ReadTokensAsync()
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), "src", "OmpOS.Native.Views", "Themes", "OmpOSTokens.axaml");
        var document = XDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true));
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in document.Root!.Descendants().Where(static element => element.Attribute(Xaml + "Key") is not null && (element.Name.LocalName is "Double" or "FontWeight")))
        {
            tokens[(string)element.Attribute(Xaml + "Key")!] = element.Value.Trim();
        }

        tokens.ShouldContainKey("OmpOS.Font.Size.DisplaySm", "the size tokens are read from the dictionary");
        return tokens;
    }

    private static IEnumerable<string> MarkupFiles()
    {
        var root = ShellHarness.RepoRoot();
        var views = Directory.EnumerateFiles(Path.Combine(root, "src", "OmpOS.Native.Views"), "*.axaml", SearchOption.AllDirectories);
        var heads = Directory.EnumerateFiles(Path.Combine(root, "apps"), "*.axaml", SearchOption.AllDirectories);
        return views.Concat(heads)
            .Where(static file => !IsBuildOutput(file) && !string.Equals(Path.GetFileName(file), "OmpOSTokens.axaml", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
    }

    private static IEnumerable<string> CodeFiles()
    {
        var root = ShellHarness.RepoRoot();
        return new[] { Path.Combine(root, "src", "OmpOS.Native.Views"), Path.Combine(root, "apps") }
            .SelectMany(static folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(static file => !IsBuildOutput(file))
            .Order(StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [GeneratedRegex("\\{(?:Dynamic|Static)Resource (?<key>[^}]+)\\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenReference();

    [GeneratedRegex("FontSize\\s*=\\s*(?:2\\d|[3-9]\\d)(?:\\.\\d+)?\\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LargeLiteralSize();

    [GeneratedRegex("new\\s+FontFamily\\s*\\(\\s*\"", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LiteralFamilyInCode();
}
