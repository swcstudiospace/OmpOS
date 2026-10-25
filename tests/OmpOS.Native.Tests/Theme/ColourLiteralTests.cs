using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Theme;

/// <summary>
/// No view holds a colour literal. A colour belongs in <c>Themes/OmpOSTokens.axaml</c>, where the theme variant can
/// change it; a view refers to it with <c>{DynamicResource ...}</c>. The scan reads <c>src/OmpOS.Native.Views/Views</c> as
/// text, so no Avalonia runtime is started.
/// </summary>
/// <remarks>
/// The rule. An attribute is colour-typed when its local name ends in <c>Brush</c> or is one of <c>Background</c>,
/// <c>Foreground</c>, <c>Fill</c>, <c>Stroke</c> or <c>Color</c>. The <c>Value</c> attribute of an element is also
/// colour-typed when the same element's <c>Property</c> names a colour attribute, which covers
/// <c>&lt;Setter Property="Background" Value="..." /&gt;</c>. Attribute values may be written in double or single
/// quotes. A value is a literal when it does not start with <c>{</c> (a markup extension) and it is either a hex colour
/// (<c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c> or <c>#AARRGGBB</c>) or the name of an Avalonia predefined colour (the
/// public <see cref="Color"/> properties of <see cref="Colors"/>, matched without regard to case). <c>Transparent</c>
/// is excluded: it is the absence of paint, it does not change with the theme, and no palette value stands for it.
/// XML comments are blanked before the scan. Colour given as element text (for example a <c>SolidColorBrush</c> element
/// holding <c>#fff</c>) is outside the rule; no view uses that form.
/// </remarks>
public sealed class ColourLiteralTests
{
    /// <summary>The most allowlist rows the repository may carry. It may only shrink; it is zero because no view holds a literal.</summary>
    private const int AllowlistBaseline = 0;

    private const string ViewsDirectory = "src/OmpOS.Native.Views/Views";
    private const string AllowlistPath = "tests/OmpOS.Native.Tests/Theme/ColourLiteralAllowlist.txt";

    private static readonly HashSet<string> ColourAttributes = new(StringComparer.Ordinal)
    {
        "Background", "Foreground", "Fill", "Stroke", "Color",
    };

    private static readonly HashSet<string> NamedColours = typeof(Colors)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(static property => property.PropertyType == typeof(Color) && !string.Equals(property.Name, "Transparent", StringComparison.Ordinal))
        .Select(static property => property.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex HexColour = new("^#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    /// <summary>One element start tag with its attributes. The quoted values are consumed whole, so a <c>&gt;</c> inside them does not end the tag.</summary>
    private static readonly Regex TagPattern = new(
        @"<(?<name>[A-Za-z_][^\s/>]*)(?:\s+[A-Za-z_][\w.:-]*\s*=\s*(?:""[^""]*""|'[^']*'))*\s*/?>",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex CommentPattern = new(
        "<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly (int Line, string Attribute, string Value)[] ExpectedRuleLiterals =
    {
        (1, "Background", "#ffffff"),
        (2, "Fill", "White"),
    };

    private static readonly (int Line, string Attribute, string Value)[] ExpectedSetterLiterals =
    {
        (1, "Value", "#ffffff"),
        (2, "Value", "White"),
        (3, "Background", "#000000"),
    };

    /// <summary>Each colour literal the rule flags in markup, with its 1-based line.</summary>
    private readonly record struct Literal(int Line, string Attribute, string Value);

    /// <summary>One attribute of a tag: its name, its unquoted value, and the index of that value in the scanned text.</summary>
    private readonly record struct TagAttribute(string Name, string Value, int ValueIndex);

    /// <summary>The rule flags a hex literal and a named colour in colour-typed attributes, and ignores bindings, comments and <c>Transparent</c>.</summary>
    [Fact]
    public void TheRuleFlagsLiteralsAndIgnoresBindingsCommentsAndTransparent()
    {
        const string markup = """
            <Border Background="#ffffff" BorderBrush="{DynamicResource OmpOS.Color.TextSecondary}" />
            <Path Fill="White" />
            <Rectangle Stroke="Transparent" Foreground="{x:Null}" />
            <!-- <Border Background="#000000" /> -->
            <TextBlock Padding="8,6" Text="Background" />
            """;

        FindLiterals(markup)
            .Select(static literal => (literal.Line, literal.Attribute, literal.Value))
            .ShouldBe(ExpectedRuleLiterals);
    }

    /// <summary>A <c>Value</c> is colour-typed inside a Setter whose <c>Property</c> is a colour; both quote styles are read.</summary>
    [Fact]
    public void TheRuleFlagsSetterValuesAndSingleQuotedLiterals()
    {
        const string markup = """
            <Setter Property="Background" Value="#ffffff" />
            <Setter Property='Foreground' Value='White' />
            <Border Background='#000000' BorderBrush='{DynamicResource OmpOS.Color.TextSecondary}' />
            <Setter Property="Width" Value="#ffffff" />
            <Setter Property="Background" Value="{DynamicResource OmpOS.Color.Surface}" />
            <TextBlock Text='Fill="#ffffff"' />
            """;

        FindLiterals(markup)
            .Select(static literal => (literal.Line, literal.Attribute, literal.Value))
            .ShouldBe(ExpectedSetterLiterals);
    }

    /// <summary>The scan reads real view files, so a pass cannot come from an empty directory.</summary>
    [Fact]
    public void TheScanReadsTheViewFiles()
    {
        ViewFiles().Count().ShouldBeGreaterThan(0, $"{ViewsDirectory} must contain .axaml views to scan");
    }

    /// <summary>A view file with a colour literal must be allowlisted; none is, because no view holds one.</summary>
    [Fact]
    public void NoViewHoldsAColourLiteralOutsideTheAllowlist()
    {
        var allowed = ReadAllowlist().Select(static row => row[0]).ToHashSet(StringComparer.Ordinal);

        var offenders = ScanViews()
            .Where(pair => !allowed.Contains(pair.Key))
            .SelectMany(pair => pair.Value.Select(literal => $"{pair.Key}:{literal.Line} {literal.Attribute}=\"{literal.Value}\""))
            .ToList();

        offenders.ShouldBeEmpty("colour literals belong in OmpOSTokens.axaml; use a DynamicResource, or allowlist the file with a reason and phase");
    }

    /// <summary>An allowlisted file must still hold a literal; a row whose file is clean is stale and must be removed.</summary>
    [Fact]
    public void EveryAllowlistedFileStillHoldsAColourLiteral()
    {
        var scanned = ScanViews();

        foreach (var row in ReadAllowlist())
        {
            scanned.ContainsKey(row[0]).ShouldBeTrue($"{row[0]} is allowlisted but holds no colour literal; remove its row");
        }
    }

    /// <summary>The allowlist may not grow past the baseline. Lower the baseline when a row is removed.</summary>
    [Fact]
    public void TheAllowlistIsNoLargerThanItsBaseline()
    {
        ReadAllowlist().Count.ShouldBeLessThanOrEqualTo(AllowlistBaseline, "the allowlist may only shrink");
    }

    /// <summary>Every allowlist row names a file, a reason and the phase that migrates it.</summary>
    [Fact]
    public void EveryAllowlistRowNamesAFileReasonAndPhase()
    {
        foreach (var row in ReadAllowlist())
        {
            row.Length.ShouldBe(3, $"row '{string.Join(" | ", row)}' must be: path | reason | phase");
            row.All(static field => field.Length > 0).ShouldBeTrue($"row '{string.Join(" | ", row)}' has an empty field");
        }
    }

    /// <summary>The views' colour literals, keyed by repository-relative path. Files without a literal are absent.</summary>
    private static Dictionary<string, List<Literal>> ScanViews()
    {
        var root = ShellHarness.RepoRoot();
        var found = new Dictionary<string, List<Literal>>(StringComparer.Ordinal);

        foreach (var file in ViewFiles())
        {
            var literals = FindLiterals(File.ReadAllText(file));
            if (literals.Count > 0)
            {
                found[Path.GetRelativePath(root, file).Replace('\\', '/')] = literals;
            }
        }

        return found;
    }

    private static IEnumerable<string> ViewFiles()
    {
        return Directory.EnumerateFiles(Path.Combine(ShellHarness.RepoRoot(), ViewsDirectory), "*.axaml", SearchOption.AllDirectories);
    }

    /// <summary>Reads the allowlist: one row per file, <c>path | reason | phase</c>; blank lines and <c>#</c> comments are skipped.</summary>
    private static List<string[]> ReadAllowlist()
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), AllowlistPath);
        File.Exists(path).ShouldBeTrue($"the allowlist must exist at {path}");

        return File.ReadLines(path)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Select(static line => line.Split('|', StringSplitOptions.TrimEntries))
            .ToList();
    }

    /// <summary>
    /// Applies the rule to markup. Comment text is blanked, keeping its line breaks, so line numbers stay true. Each
    /// start tag is read whole, so a Setter's <c>Value</c> is judged by the <c>Property</c> of the same element.
    /// </summary>
    private static List<Literal> FindLiterals(string markup)
    {
        var text = CommentPattern.Replace(markup, static match => new string(match.Value.Select(static c => c == '\n' ? '\n' : ' ').ToArray()));
        var literals = new List<Literal>();

        foreach (Match tag in TagPattern.Matches(text))
        {
            var attributes = ReadAttributes(tag);
            var setterSetsColour = attributes.Any(static attribute =>
                string.Equals(LocalName(attribute.Name), "Property", StringComparison.Ordinal) && IsColourAttribute(attribute.Value.Trim()));

            foreach (var attribute in attributes)
            {
                var name = LocalName(attribute.Name);
                var colourTyped = IsColourAttribute(name) || (string.Equals(name, "Value", StringComparison.Ordinal) && setterSetsColour);
                var value = attribute.Value.Trim();

                if (colourTyped && IsLiteral(value))
                {
                    literals.Add(new Literal(LineOf(text, attribute.ValueIndex), name, value));
                }
            }
        }

        return literals;
    }

    /// <summary>
    /// Reads the attributes of one start tag in document order, starting after the element name. An attribute is
    /// <c>\s+name\s*=\s*"value"</c> or <c>\s+name\s*=\s*'value'</c>, where the name is <c>[A-Za-z_][\w.:-]*</c>. The
    /// scan reads one character at a time and stops at the first text that is not an attribute, so it runs in linear time.
    /// </summary>
    private static List<TagAttribute> ReadAttributes(Match tag)
    {
        var attributes = new List<TagAttribute>();
        var text = tag.Value;
        var name = tag.Groups["name"];
        var position = name.Index + name.Length - tag.Index;

        while (true)
        {
            var cursor = SkipWhitespace(text, position);
            if (cursor == position || cursor >= text.Length || !IsNameStart(text[cursor]))
            {
                break;
            }

            var nameStart = cursor;
            cursor = SkipNameCharacters(text, cursor);
            var attributeName = text[nameStart..cursor];

            cursor = SkipWhitespace(text, cursor);
            if (cursor >= text.Length || text[cursor] != '=')
            {
                break;
            }

            cursor = SkipWhitespace(text, cursor + 1);
            if (cursor >= text.Length || text[cursor] is not ('"' or '\''))
            {
                break;
            }

            var valueStart = cursor + 1;
            var valueEnd = text.IndexOf(text[cursor], valueStart);
            if (valueEnd < 0)
            {
                break;
            }

            attributes.Add(new TagAttribute(attributeName, text[valueStart..valueEnd], tag.Index + valueStart));
            position = valueEnd + 1;
        }

        return attributes;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int SkipNameCharacters(string text, int index)
    {
        while (index < text.Length && IsNameCharacter(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsNameStart(char c)
    {
        return c is '_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
    }

    /// <summary>A character after the first of a name: <c>[\w.:-]</c>.</summary>
    private static bool IsNameCharacter(char c)
    {
        return (c is '.' or ':' or '-') || IsWordCharacter(c);
    }

    /// <summary>A word character as <c>\w</c> matches it: <c>[\p{L}\p{Mn}\p{Nd}\p{Pc}]</c>.</summary>
    private static bool IsWordCharacter(char c)
    {
        return char.GetUnicodeCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.NonSpacingMark or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
    }

    private static string LocalName(string name)
    {
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? name : name[(colon + 1)..];
    }

    private static bool IsColourAttribute(string attribute)
    {
        return attribute.EndsWith("Brush", StringComparison.Ordinal) || ColourAttributes.Contains(attribute);
    }

    private static bool IsLiteral(string value)
    {
        return !value.StartsWith('{') && (HexColour.IsMatch(value) || NamedColours.Contains(value));
    }

    private static int LineOf(string text, int index)
    {
        return text[..index].Count(static c => c == '\n') + 1;
    }
}
