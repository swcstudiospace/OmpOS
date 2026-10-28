using System.Globalization;
using System.Text;

namespace Autonogrammer.Workspace.Conventions;

/// <summary>
/// Reads the lines between the two <c>---</c> fences. Only top-level keys are kept: plain, quoted and block (<c>|</c>, <c>&gt;</c>) scalars, flow lists
/// (<c>[a, b]</c>) and block lists (<c>- item</c>). Nested maps are skipped. YAML anchors, aliases, tags and merge keys are not interpreted: their text is
/// kept as written, so no value can expand into another.
/// </summary>
internal sealed class ConventionFrontmatterBlock
{
    private const int MaxKeys = 100;
    private const int MaxLineLength = 4096;

    private readonly Dictionary<string, ConventionFieldBuilder> _fields = new(StringComparer.Ordinal);
    private ConventionFieldBuilder? _current;
    private string? _problem;

    private ConventionFrontmatterBlock()
    {
    }

    /// <summary>Reads a block of lines.</summary>
    /// <param name="lines">All the lines of the file.</param>
    /// <param name="start">The index of the first line inside the fences.</param>
    /// <param name="end">The index of the closing fence.</param>
    /// <returns>The fields and the first problem found, if any.</returns>
    public static (IReadOnlyDictionary<string, ConventionFieldValue> Fields, string? Problem) Read(IReadOnlyList<string> lines, int start, int end)
    {
        var block = new ConventionFrontmatterBlock();
        for (var index = start; index < end && block._problem is null; index++)
        {
            block.ReadLine(lines[index].TrimEnd('\r'), index + 1);
        }

        if (block._problem is not null)
        {
            return (new Dictionary<string, ConventionFieldValue>(StringComparer.Ordinal), block._problem);
        }

        var fields = new Dictionary<string, ConventionFieldValue>(block._fields.Count, StringComparer.Ordinal);
        foreach (var (key, builder) in block._fields)
        {
            if (builder.Mode != ConventionFieldMode.Nested)
            {
                fields[key] = builder.Build();
            }
        }

        return (fields, null);
    }

    private static string Describe(int lineNumber, string text) =>
        string.Create(CultureInfo.InvariantCulture, $"line {lineNumber}: {text}");

    private static int FindKeySeparator(string line)
    {
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == ':' && (index == line.Length - 1 || char.IsWhiteSpace(line[index + 1])))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsKey(string key)
    {
        if (key.Length == 0)
        {
            return false;
        }

        foreach (var character in key)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadScalar(string text)
    {
        if (text.Length >= 2 && text[0] == '"')
        {
            var closing = FindClosingDoubleQuote(text);
            return closing > 0 ? Unescape(text.AsSpan(1, closing - 1)) : text;
        }

        if (text.Length >= 2 && text[0] == '\'')
        {
            var closing = FindClosingSingleQuote(text);
            return closing > 0 ? text[1..closing].Replace("''", "'", StringComparison.Ordinal) : text;
        }

        var comment = text.IndexOf(" #", StringComparison.Ordinal);
        return (comment >= 0 ? text[..comment] : text).Trim();
    }

    private static int FindClosingDoubleQuote(string text)
    {
        for (var index = 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == '"')
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindClosingSingleQuote(string text)
    {
        for (var index = 1; index < text.Length; index++)
        {
            if (text[index] != '\'')
            {
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '\'')
            {
                index++;
                continue;
            }

            return index;
        }

        return -1;
    }

    private static string Unescape(ReadOnlySpan<char> text)
    {
        var result = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\' || index + 1 >= text.Length)
            {
                result.Append(text[index]);
                continue;
            }

            index++;
            result.Append(text[index] switch
            {
                'n' => '\n',
                't' => '\t',
                _ => text[index],
            });
        }

        return result.ToString();
    }

    private static List<string> SplitFlowList(string inner)
    {
        var items = new List<string>();
        var item = new StringBuilder();
        var quote = '\0';
        foreach (var character in inner)
        {
            if (quote != '\0')
            {
                item.Append(character);
                quote = character == quote ? '\0' : quote;
            }
            else if (character is '"' or '\'')
            {
                item.Append(character);
                quote = character;
            }
            else if (character == ',')
            {
                items.Add(ReadScalar(item.ToString().Trim()));
                item.Clear();
            }
            else
            {
                item.Append(character);
            }
        }

        var last = item.ToString().Trim();
        if (last.Length > 0)
        {
            items.Add(ReadScalar(last));
        }

        return items;
    }

    private void ReadLine(string line, int number)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return;
        }

        if (line.Length > MaxLineLength)
        {
            _problem = Describe(number, "the line is too long");
        }
        else if (line[0] is ' ' or '\t')
        {
            ReadIndented(trimmed, number);
        }
        else
        {
            ReadKey(line, number);
        }
    }

    private void ReadIndented(string text, int number)
    {
        if (_current is null)
        {
            _problem = Describe(number, "an indented line has no key above it");
            return;
        }

        if (_current.Mode == ConventionFieldMode.Block)
        {
            _current.AppendBlockLine(text);
            return;
        }

        if (_current.Mode != ConventionFieldMode.Nested && (text.StartsWith("- ", StringComparison.Ordinal) || text.Length == 1 && text[0] == '-'))
        {
            _current.Mode = ConventionFieldMode.List;
            _current.AddItem(text.Length > 1 ? ReadScalar(text[2..].Trim()) : string.Empty);
            return;
        }

        if (_current.Mode == ConventionFieldMode.Scalar && _current.HasText)
        {
            _current.AppendContinuation(text);
        }
        else if (_current.Mode == ConventionFieldMode.Scalar)
        {
            _current.Mode = ConventionFieldMode.Nested;
        }
    }

    private void ReadKey(string line, int number)
    {
        var colon = FindKeySeparator(line);
        var key = colon > 0 ? line[..colon].Trim() : string.Empty;
        if (!IsKey(key))
        {
            _problem = Describe(number, "the line is not 'key: value'");
        }
        else if (_fields.ContainsKey(key))
        {
            _problem = Describe(number, "the key '" + key + "' appears twice");
        }
        else if (_fields.Count >= MaxKeys)
        {
            _problem = Describe(number, "there are too many keys");
        }
        else
        {
            var builder = CreateBuilder(line[(colon + 1)..].Trim(), number);
            _fields[key] = builder;
            _current = builder;
        }
    }

    private ConventionFieldBuilder CreateBuilder(string rest, int number)
    {
        if (rest.Length == 0)
        {
            return new ConventionFieldBuilder(ConventionFieldMode.Scalar, folded: false);
        }

        if (rest[0] is '|' or '>')
        {
            return new ConventionFieldBuilder(ConventionFieldMode.Block, folded: rest[0] == '>');
        }

        if (rest[0] == '{')
        {
            return new ConventionFieldBuilder(ConventionFieldMode.Nested, folded: false);
        }

        if (rest[0] == '[')
        {
            var list = new ConventionFieldBuilder(ConventionFieldMode.List, folded: false);
            if (rest[^1] == ']')
            {
                list.AddItems(SplitFlowList(rest[1..^1]));
            }
            else
            {
                _problem = Describe(number, "the list is not closed with ']'");
            }

            return list;
        }

        var scalar = new ConventionFieldBuilder(ConventionFieldMode.Scalar, folded: false);
        scalar.SetScalar(ReadScalar(rest));
        return scalar;
    }
}
