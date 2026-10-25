using System.Globalization;
using System.Text;

namespace OmpOS.Native.Views.Hooks;

/// <summary>
/// Renders a hook's argument list as text. An argument is quoted only when it would otherwise read as more than one
/// argument, or as nothing, so the displayed command line is unambiguous. Quoting escapes backslashes, quotes and control
/// characters, so every argument can be recovered from its text.
/// </summary>
internal static class HookArgumentText
{
    /// <summary>Renders an argument list as one command line, arguments separated by single spaces.</summary>
    /// <param name="arguments">The argument list. The first element is the executable.</param>
    /// <returns>The command line.</returns>
    public static string CommandLine(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return string.Join(' ', arguments.Select(Quote));
    }

    /// <summary>Renders an argument with its position, one per line, for the consent panel.</summary>
    /// <param name="arguments">The argument list. The first element is the executable.</param>
    /// <returns>One line per argument, each starting with its zero-based index.</returns>
    public static string Numbered(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return string.Join(
            '\n',
            arguments.Select((argument, index) => index.ToString(CultureInfo.InvariantCulture) + ": " + Quote(argument)));
    }

    /// <summary>Renders one argument, quoted when it needs quotes to read back as the same argument.</summary>
    /// <param name="argument">The argument.</param>
    /// <returns>The argument text.</returns>
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Length > 0 && !argument.Any(NeedsQuotes))
        {
            return argument;
        }

        var builder = new StringBuilder(argument.Length + 2).Append('"');
        foreach (var character in argument)
        {
            _ = character switch
            {
                '"' => builder.Append("\\\""),
                '\\' => builder.Append("\\\\"),
                '\n' => builder.Append("\\n"),
                '\r' => builder.Append("\\r"),
                '\t' => builder.Append("\\t"),
                _ when char.IsControl(character) => builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)),
                _ => builder.Append(character),
            };
        }

        return builder.Append('"').ToString();
    }

    private static bool NeedsQuotes(char character) =>
        char.IsWhiteSpace(character) || char.IsControl(character) || character is '"' or '\\';
}
