using System.Globalization;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Reads the semantic versions that omp and Bun publish, and the space-separated comparator ranges that npm's
/// <c>engines</c> field uses for them, such as <c>&gt;=1.4.4</c>. Alternation (<c>||</c>) and caret or tilde ranges are not
/// supported; a range that uses them reads as unknown.
/// </summary>
internal static class OmpSemver
{
    private static readonly string[] Operators = [">=", "<=", ">", "<", "="];

    /// <summary>A parsed <c>MAJOR.MINOR.PATCH[-PRERELEASE]</c> version.</summary>
    internal readonly record struct Parsed(int Major, int Minor, int Patch, string? Prerelease);

    /// <summary>
    /// Parses a plain version. Build metadata and any character other than a letter, digit, dot or hyphen are rejected, so
    /// a parsed version is always safe to use as a directory name.
    /// </summary>
    /// <param name="text">The version text.</param>
    /// <param name="version">The parsed version when the result is true.</param>
    /// <returns>True when the text is a plain semantic version.</returns>
    public static bool TryParse(string? text, out Parsed version)
    {
        version = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var dash = text.IndexOf('-', StringComparison.Ordinal);
        var core = dash < 0 ? text : text[..dash];
        var prerelease = dash < 0 ? null : text[(dash + 1)..];
        if (prerelease is not null && !IsPrerelease(prerelease))
        {
            return false;
        }

        var parts = core.Split('.');
        if (parts.Length != 3 || !TryNumber(parts[0], out var major) || !TryNumber(parts[1], out var minor) || !TryNumber(parts[2], out var patch))
        {
            return false;
        }

        version = new Parsed(major, minor, patch, prerelease);
        return true;
    }

    /// <summary>Orders two versions by semantic precedence. A release sorts above its prerelease.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns>Negative, zero or positive as <paramref name="left"/> sorts before, equal to, or after <paramref name="right"/>.</returns>
    public static int Compare(Parsed left, Parsed right)
    {
        var order = left.Major.CompareTo(right.Major);
        if (order == 0)
        {
            order = left.Minor.CompareTo(right.Minor);
        }

        if (order == 0)
        {
            order = left.Patch.CompareTo(right.Patch);
        }

        if (order != 0)
        {
            return order;
        }

        if (left.Prerelease is null)
        {
            return right.Prerelease is null ? 0 : 1;
        }

        if (right.Prerelease is null)
        {
            return -1;
        }

        return string.CompareOrdinal(left.Prerelease, right.Prerelease);
    }

    /// <summary>Checks a version against a range of space-separated comparators, all of which must hold.</summary>
    /// <param name="range">The range, such as <c>&gt;=1.4.4 &lt;2.0.0</c>.</param>
    /// <param name="version">The version to test.</param>
    /// <returns>True or false when the range is understood; null when it is not.</returns>
    public static bool? SatisfiesRange(string range, Parsed version)
    {
        var tokens = range.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < tokens.Length; index++)
        {
            var op = OperatorOf(tokens[index], out var operand);
            if (operand.Length == 0)
            {
                if (index + 1 >= tokens.Length)
                {
                    return null;
                }

                operand = tokens[++index];
            }

            if (!TryParse(operand, out var bound))
            {
                return null;
            }

            if (!Holds(op, Compare(version, bound)))
            {
                return false;
            }
        }

        return true;
    }

    private static string OperatorOf(string token, out string operand)
    {
        foreach (var op in Operators)
        {
            if (token.StartsWith(op, StringComparison.Ordinal))
            {
                operand = token[op.Length..];
                return op;
            }
        }

        operand = token;
        return "=";
    }

    private static bool Holds(string op, int comparison) => op switch
    {
        ">=" => comparison >= 0,
        ">" => comparison > 0,
        "<=" => comparison <= 0,
        "<" => comparison < 0,
        _ => comparison == 0,
    };

    private static bool IsPrerelease(string text) =>
        text.Length > 0 && text[0] != '.' && text.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');

    private static bool TryNumber(string text, out int value)
    {
        value = 0;
        return text.Length > 0 && text.All(char.IsAsciiDigit) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
