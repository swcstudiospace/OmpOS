using System.Globalization;
using System.Text;

namespace Autonogrammer.Presentation.Commands;

/// <summary>
/// Splits the text typed after a custom command and fills the command's prompt template, with the same placeholders omp uses for its own file commands:
/// <c>$ARGUMENTS</c> and <c>$@</c> (every argument), <c>$1</c>, <c>$2</c> and so on (one argument), <c>$@[2]</c> (from the second argument) and <c>$@[2:3]</c> (three arguments from the second).
/// </summary>
public static class SlashCommandArguments
{
    private const string AllArguments = "$ARGUMENTS";

    /// <summary>Splits typed text into arguments on white space. Single or double quotes keep a quoted run together, and the quotes are not part of the argument.</summary>
    /// <param name="text">The text typed after the command name.</param>
    /// <returns>The arguments; empty when the text is blank.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var arguments = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var started = false;
        foreach (var character in text)
        {
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character is '"' or '\'')
            {
                quote = character;
                started = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (started)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(character);
                started = true;
            }
        }

        if (started)
        {
            arguments.Add(current.ToString());
        }

        return arguments;
    }

    /// <summary>Fills a prompt template. A template with no placeholder at all gets the typed text appended after a blank line, so the arguments are never lost.</summary>
    /// <param name="template">The body of a command file.</param>
    /// <param name="typedText">The text typed after the command name.</param>
    /// <returns>The prompt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="template"/> or <paramref name="typedText"/> is null.</exception>
    public static string Expand(string template, string typedText)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(typedText);

        var arguments = Split(typedText);
        var result = new StringBuilder(template.Length + typedText.Length);
        var usedPlaceholder = false;
        var index = 0;
        while (index < template.Length)
        {
            var length = TryReadPlaceholder(template, index, arguments, out var value);
            if (length == 0)
            {
                result.Append(template[index]);
                index++;
            }
            else
            {
                result.Append(value);
                usedPlaceholder = true;
                index += length;
            }
        }

        var trimmed = typedText.Trim();
        return usedPlaceholder || trimmed.Length == 0 ? result.ToString() : result.ToString().TrimEnd() + "\n\n" + trimmed;
    }

    /// <summary>Reads one placeholder at a position.</summary>
    /// <returns>The number of characters it covers, or zero when none starts there.</returns>
    private static int TryReadPlaceholder(string template, int index, IReadOnlyList<string> arguments, out string value)
    {
        value = string.Empty;
        if (template[index] != '$')
        {
            return 0;
        }

        var rest = template.AsSpan(index);
        if (rest.StartsWith(AllArguments, StringComparison.Ordinal))
        {
            value = string.Join(' ', arguments);
            return AllArguments.Length;
        }

        if (rest.StartsWith("$@[", StringComparison.Ordinal))
        {
            var length = TryReadSlice(rest, arguments, out value);
            if (length > 0)
            {
                return length;
            }
        }

        if (rest.StartsWith("$@", StringComparison.Ordinal))
        {
            value = string.Join(' ', arguments);
            return 2;
        }

        var digits = CountDigits(rest[1..]);
        if (digits is 0 or > 9)
        {
            return 0;
        }

        var position = int.Parse(rest.Slice(1, digits), NumberStyles.None, CultureInfo.InvariantCulture);
        value = position >= 1 && position <= arguments.Count ? arguments[position - 1] : string.Empty;
        return digits + 1;
    }

    /// <summary>Reads <c>$@[start]</c> or <c>$@[start:length]</c> (the length may be empty).</summary>
    private static int TryReadSlice(ReadOnlySpan<char> rest, IReadOnlyList<string> arguments, out string value)
    {
        value = string.Empty;
        var start = CountDigits(rest[3..]);
        if (start == 0 || start > 9)
        {
            return 0;
        }

        var cursor = 3 + start;
        var first = int.Parse(rest.Slice(3, start), NumberStyles.None, CultureInfo.InvariantCulture);
        int? take = null;
        if (cursor < rest.Length && rest[cursor] == ':')
        {
            cursor++;
            var lengthDigits = CountDigits(rest[cursor..]);
            if (lengthDigits > 9)
            {
                return 0;
            }

            if (lengthDigits > 0)
            {
                take = int.Parse(rest.Slice(cursor, lengthDigits), NumberStyles.None, CultureInfo.InvariantCulture);
            }

            cursor += lengthDigits;
        }

        if (cursor >= rest.Length || rest[cursor] != ']')
        {
            return 0;
        }

        value = Slice(arguments, first, take);
        return cursor + 1;
    }

    private static string Slice(IReadOnlyList<string> arguments, int first, int? take)
    {
        if (first < 1 || first > arguments.Count || take is <= 0)
        {
            return string.Empty;
        }

        var count = take is { } limit ? Math.Min(limit, arguments.Count - first + 1) : arguments.Count - first + 1;
        return string.Join(' ', arguments.Skip(first - 1).Take(count));
    }

    private static int CountDigits(ReadOnlySpan<char> text)
    {
        var count = 0;
        while (count < text.Length && char.IsAsciiDigit(text[count]))
        {
            count++;
        }

        return count > 9 ? 10 : count;
    }
}
