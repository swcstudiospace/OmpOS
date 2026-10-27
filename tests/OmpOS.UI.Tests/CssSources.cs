using System.Globalization;
using System.Text.RegularExpressions;
using Shouldly;

namespace OmpOS.UI.Tests;

/// <summary>Locates the web head's stylesheets and reads their design tokens, so the contract tests work from the shipped files.</summary>
internal static partial class CssSources
{
    /// <summary>The three theme token sets tokens.css declares.</summary>
    internal enum Theme
    {
        /// <summary>The default set on <c>:root</c>.</summary>
        Light,

        /// <summary>The set under <c>[data-theme='dark']</c>.</summary>
        Dark,

        /// <summary>The set under <c>[data-theme='system']</c> inside the dark media query.</summary>
        System,
    }

    /// <summary>Gets the repository root, found by walking up to OmpOS.slnx.</summary>
    internal static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
            {
                directory = directory.Parent;
            }

            directory.ShouldNotBeNull("OmpOS.slnx must be reachable by walking up from the test output");
            return directory.FullName;
        }
    }

    /// <summary>Gets the directory of the shared UI stylesheets.</summary>
    internal static string StylesDirectory => Path.Combine(RepoRoot, "src", "OmpOS.UI", "wwwroot", "styles");

    /// <summary>Gets the directory of the font files and their licences.</summary>
    internal static string FontsDirectory => Path.Combine(RepoRoot, "src", "OmpOS.UI", "wwwroot", "fonts");

    /// <summary>Gets the web head's own stylesheet directory.</summary>
    internal static string WebCssDirectory => Path.Combine(RepoRoot, "apps", "OmpOS.Web", "wwwroot", "css");

    /// <summary>Gets every stylesheet the web head ships: the shared UI styles and the head's own boot styles.</summary>
    /// <returns>The absolute paths, ordered.</returns>
    internal static IReadOnlyList<string> AllCssFiles()
    {
        var files = Directory.GetFiles(StylesDirectory, "*.css").ToList();
        if (Directory.Exists(WebCssDirectory))
        {
            files.AddRange(Directory.GetFiles(WebCssDirectory, "*.css"));
        }

        files.Sort(StringComparer.Ordinal);
        files.ShouldNotBeEmpty("the web head must ship stylesheets");
        return files;
    }

    /// <summary>Reads one stylesheet of the shared UI styles.</summary>
    /// <param name="name">The file name, for example <c>tokens.css</c>.</param>
    /// <returns>The file text.</returns>
    internal static string ReadStyle(string name)
    {
        var path = Path.Combine(StylesDirectory, name);
        File.Exists(path).ShouldBeTrue($"expected the stylesheet {path}");
        return File.ReadAllText(path);
    }

    /// <summary>Removes the comments of a stylesheet.</summary>
    /// <param name="css">The stylesheet text.</param>
    /// <returns>The text without <c>/* ... */</c> comments.</returns>
    internal static string StripComments(string css) => CommentPattern().Replace(css, string.Empty);

    /// <summary>Gets the declaration block (with its comments) of one theme in tokens.css.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The text between the braces.</returns>
    internal static string ThemeBlock(Theme theme)
    {
        var tokens = ReadStyle("tokens.css");
        var match = theme switch
        {
            Theme.Light => LightBlock().Match(tokens),
            Theme.Dark => DarkBlock().Match(tokens),
            _ => SystemBlock().Match(tokens),
        };
        match.Success.ShouldBeTrue($"tokens.css must declare the {theme} token block");
        return match.Groups["body"].Value;
    }

    /// <summary>Gets the tokens a theme declares itself, without the Light values it inherits.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>Token name to value.</returns>
    internal static IReadOnlyDictionary<string, string> OwnTokens(Theme theme) => ParseDeclarations(StripComments(ThemeBlock(theme)));

    /// <summary>Gets the tokens in force under a theme: the Light set, overlaid with the theme's own values.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>Token name to value.</returns>
    internal static IReadOnlyDictionary<string, string> EffectiveTokens(Theme theme)
    {
        var effective = new Dictionary<string, string>(OwnTokens(Theme.Light), StringComparer.Ordinal);
        if (theme != Theme.Light)
        {
            foreach (var (name, value) in OwnTokens(theme))
            {
                effective[name] = value;
            }
        }

        return effective;
    }

    /// <summary>Resolves a colour token under a theme to its hex value.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="token">The token name, for example <c>--ompos-text</c>.</param>
    /// <returns>The <c>#rrggbb</c> value.</returns>
    internal static string Hex(Theme theme, string token)
    {
        EffectiveTokens(theme).TryGetValue(token, out var value).ShouldBeTrue($"{token} must be defined for {theme}");
        HexColour().IsMatch(value!).ShouldBeTrue($"{token} must be a #rrggbb colour for {theme}, found {value}");
        return value!;
    }

    /// <summary>Computes the WCAG 2 contrast ratio of two <c>#rrggbb</c> colours.</summary>
    /// <param name="foreground">The first colour.</param>
    /// <param name="background">The second colour.</param>
    /// <returns>The ratio, at least 1.</returns>
    internal static double ContrastRatio(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static Dictionary<string, string> ParseDeclarations(string css)
    {
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Declaration().Matches(css))
        {
            declarations[match.Groups["name"].Value] = match.Groups["value"].Value.Trim();
        }

        return declarations;
    }

    private static double RelativeLuminance(string hex)
    {
        double Channel(int offset)
        {
            var value = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(1)) + (0.7152 * Channel(3)) + (0.0722 * Channel(5));
    }

    [GeneratedRegex("/\\*.*?\\*/", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex CommentPattern();

    [GeneratedRegex("^:root\\s*\\{(?<body>[^{}]*)\\}", RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LightBlock();

    [GeneratedRegex("^:root\\[data-theme='dark'\\]\\s*\\{(?<body>[^{}]*)\\}", RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DarkBlock();

    [GeneratedRegex("^\\s*:root\\[data-theme='system'\\]\\s*\\{(?<body>[^{}]*)\\}", RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SystemBlock();

    [GeneratedRegex("(?<name>--ompos-[a-z0-9-]+)\\s*:\\s*(?<value>[^;]+);", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Declaration();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HexColour();
}
