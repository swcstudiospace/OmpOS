using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>Rules on the Settings markup and code that no headless render shows: no literal font, colour or copy, tokens that exist, compiled bindings, no animation.</summary>
public sealed class SettingsMarkupTests
{
    private const string SettingsDirectory = "src/Autonogrammer.Native.Views/Views/Settings";
    private const string TokensPath = "src/Autonogrammer.Native.Views/Themes/AutonogrammerTokens.axaml";

    private static readonly Regex LiteralCopy = new(
        "(?:\\bText|\\bContent|\\bHeader|\\bPlaceholderText|AutomationProperties\\.Name|AutomationProperties\\.HelpText|ToolTip\\.Tip)=\"(?<value>[^{\"][^\"]*)\"",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralFontAttribute = new("\\bFontFamily=\"(?<value>[^{\"][^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex FontSetter = new(
        "Property=\"FontFamily\"\\s+Value=\"(?<value>[^\"]*)\"",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex HexLiteral = new("(?:^|[^&\\w#])(?<hex>#[0-9a-fA-F]{3,8})\\b", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex ColourInCode = new(
        "\\b(?:Colors\\.|Brushes\\.|Color\\.Parse|Color\\.FromRgb|Color\\.FromArgb|new\\s+Color\\(|new\\s+SolidColorBrush|SolidColorBrush\\.Parse|Brush\\.Parse)",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex FontInCode = new(
        "\\b(?:new\\s+FontFamily\\s*\\(|FontFamily\\.Parse|FontFamily\\s*=|SetFontFamily)",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex ResourceKey = new("\\{DynamicResource\\s+(?<key>[^}\\s]+)\\}", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex DefinedKey = new("x:Key=\"(?<key>[^\"]+)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex StringLiteral = new("\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    /// <summary>The only string literals the Settings code may hold: style class names and the column list of one grid. Words live in the copy classes.</summary>
    private static readonly HashSet<string> AllowedCodeLiterals = new(StringComparer.Ordinal)
    {
        "\"use-default\"",
        "\"use-template\"",
        "\"tone-success\"",
        "\"tone-error\"",
        "\"tone-secondary\"",
        "\"settings-label\"",
        "\"settings-help\"",
        "\"settings-dirty-marker\"",
        "\"*,Auto\"",
        "\"recent-load\"",
        "\"muted settings-body\"",
        "\"settings-mono\"",
        "\"settings-row\"",
        "\"tone-warning settings-help\"",
        "\"Autonogrammer.Size.TargetMin\"",
    };

    private static string SettingsPath() => Path.Combine(ShellHarness.RepoRoot(), SettingsDirectory);

    private static string WithoutXmlComments(string text) =>
        Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static string WithoutCodeComments(string text) =>
        string.Join('\n', text.Split('\n').Where(static line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static IEnumerable<(string File, string Text)> Markup() =>
        Directory.EnumerateFiles(SettingsPath(), "*.axaml").Select(static file => (Path.GetFileName(file), WithoutXmlComments(File.ReadAllText(file))));

    private static IEnumerable<(string File, string Text)> Code() =>
        Directory.EnumerateFiles(SettingsPath(), "*.cs").Select(static file => (Path.GetFileName(file), WithoutCodeComments(File.ReadAllText(file))));

    /// <summary>The scan reads the real Settings files, so a pass cannot come from an empty directory.</summary>
    [Fact]
    public void TheScanReadsTheSettingsFiles()
    {
        Markup().Count().ShouldBeGreaterThanOrEqualTo(11);
        Code().Count().ShouldBeGreaterThanOrEqualTo(15);
    }

    /// <summary>No Settings markup names a font family: families come only from the <c>Autonogrammer.Font.*</c> tokens, in attributes and in style setters.</summary>
    [Fact]
    public void NoSettingsMarkupHoldsALiteralFontFamily()
    {
        var literals = Markup()
            .SelectMany(file => LiteralFontAttribute.Matches(file.Text).Select(match => $"{file.File}: {match.Groups["value"].Value}")
                .Concat(FontSetter.Matches(file.Text)
                    .Where(static match => !match.Groups["value"].Value.StartsWith("{DynamicResource Autonogrammer.Font.", StringComparison.Ordinal))
                    .Select(match => $"{file.File}: {match.Groups["value"].Value}")))
            .ToList();

        literals.ShouldBeEmpty();
    }

    /// <summary>No Settings code builds a font family or a colour: everything is read from a token through a style.</summary>
    [Fact]
    public void NoSettingsCodeBuildsAFontOrAColour()
    {
        var offenders = Code()
            .SelectMany(file => FontInCode.Matches(file.Text).Concat(ColourInCode.Matches(file.Text)).Select(match => $"{file.File}: {match.Value}"))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    /// <summary>No Settings file holds a hex colour; the only <c>#</c> in the markup is a character reference.</summary>
    [Fact]
    public void NoSettingsFileHoldsAHexColour()
    {
        var offenders = Markup().Concat(Code())
            .SelectMany(file => HexLiteral.Matches(file.Text).Select(match => $"{file.File}: {match.Groups["hex"].Value}"))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    /// <summary>No Settings markup writes a sentence or a name as a literal: every one is read from a copy class or bound to a model.</summary>
    [Fact]
    public void NoSettingsMarkupHoldsLiteralCopy()
    {
        var literals = Markup()
            .SelectMany(file => LiteralCopy.Matches(file.Text).Select(match => $"{file.File}: {match.Groups["value"].Value}"))
            .ToList();

        literals.ShouldBeEmpty();
    }

    /// <summary>No Settings code holds a word: its string literals are style class names and one column list, and the view copy class is the one place that holds the view-only words.</summary>
    [Fact]
    public void NoSettingsCodeHoldsLiteralCopy()
    {
        var literals = Code()
            .Where(static file => !string.Equals(file.File, "SettingsViewCopy.cs", StringComparison.Ordinal))
            .SelectMany(file => file.Text.Split('\n')
                .Where(static line => !line.Contains("throw new", StringComparison.Ordinal))
                .SelectMany(line => StringLiteral.Matches(line).Select(match => match.Value))
                .Where(static literal => !AllowedCodeLiterals.Contains(literal))
                .Select(literal => $"{file.File}: {literal}"))
            .ToList();

        literals.ShouldBeEmpty();
    }

    /// <summary>Every <c>Autonogrammer.*</c> resource a Settings file refers to is defined in the token file, so a typo cannot draw nothing.</summary>
    [Fact]
    public void EveryTokenTheSettingsFilesUseExists()
    {
        var defined = DefinedKey.Matches(File.ReadAllText(Path.Combine(ShellHarness.RepoRoot(), TokensPath)))
            .Select(static match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Markup()
            .SelectMany(file => ResourceKey.Matches(file.Text).Select(match => (file.File, Key: match.Groups["key"].Value)))
            .Where(use => !defined.Contains(use.Key))
            .Select(static use => $"{use.File}: {use.Key}")
            .ToList();

        defined.ShouldContain("Autonogrammer.Font.Mono", StringComparer.Ordinal);
        missing.ShouldBeEmpty();
    }

    /// <summary>Every Settings view that binds declares the type it binds to, so its bindings compile; the page views do not bind through reflection.</summary>
    [Fact]
    public void EveryBindingViewDeclaresItsDataType()
    {
        var missing = Markup()
            .Where(static file => file.Text.Contains("{CompiledBinding", StringComparison.Ordinal) && !file.Text.Contains("x:DataType=\"", StringComparison.Ordinal))
            .Select(static file => file.File)
            .ToList();

        missing.ShouldBeEmpty();
        Markup().Where(static file => file.Text.Contains("{Binding", StringComparison.Ordinal)).ShouldBeEmpty("a view must use compiled bindings only");
    }

    /// <summary>The Settings styles use tokens for focus and surfaces and hold no animation, transition or literal motion.</summary>
    [Fact]
    public void TheSettingsStylesHoldNoAnimation()
    {
        var styles = Markup().Single(static file => string.Equals(file.File, "SettingsStyles.axaml", StringComparison.Ordinal)).Text;

        styles.ShouldContain("Autonogrammer.Color.SurfaceRaised", Case.Sensitive);
        styles.ShouldContain("Autonogrammer.Font.Mono", Case.Sensitive);
        styles.ShouldContain("Autonogrammer.Font.Display", Case.Sensitive);
        styles.ShouldNotContain("Animation", Case.Sensitive);
        styles.ShouldNotContain("Transition", Case.Sensitive);
        Markup().Where(static file => file.Text.Contains("Transitions", StringComparison.Ordinal) || file.Text.Contains("<Animation", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    /// <summary>The API key entry is masked in its markup, and neither the markup nor the code has a reveal control.</summary>
    [Fact]
    public void TheKeyEntryIsMaskedInMarkupAndHasNoRevealSwitch()
    {
        var card = Markup().Single(static file => string.Equals(file.File, "ProviderCardView.axaml", StringComparison.Ordinal)).Text;

        card.ShouldContain("PasswordChar=\"&#8226;\"", Case.Sensitive);
        card.ShouldNotContain("RevealPassword", Case.Sensitive);
        card.ShouldNotContain("revealPasswordButton", Case.Sensitive);
        Code().Where(static file => file.Text.Contains("RevealPassword", StringComparison.Ordinal)).ShouldBeEmpty();
    }
}
