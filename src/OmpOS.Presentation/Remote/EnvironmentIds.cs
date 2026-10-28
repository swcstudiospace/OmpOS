using System.Globalization;
using System.Text;

namespace OmpOS.Presentation.Remote;

/// <summary>Derives the stable identifier of a new environment profile from its name and a prefix, and makes it unique among the identifiers already taken.</summary>
internal static class EnvironmentIds
{
    private const int MaxIdLength = 64;

    /// <summary>Makes an identifier: the prefix and the name with every character that is not a letter, digit, dot, underscore or dash turned into a dash, then a numeric suffix until it is free.</summary>
    /// <param name="prefix">The prefix, for example <c>container-</c>.</param>
    /// <param name="name">The container name or distribution name.</param>
    /// <param name="taken">The identifiers in use.</param>
    /// <returns>An identifier of at most 64 characters that is not in <paramref name="taken"/>.</returns>
    internal static string Make(string prefix, string name, IReadOnlySet<string> taken)
    {
        var builder = new StringBuilder(prefix);
        foreach (var original in name)
        {
            var character = char.ToLowerInvariant(original);
            _ = builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '-');
        }

        var seed = builder.Length > MaxIdLength ? builder.ToString(0, MaxIdLength) : builder.ToString();
        var candidate = seed;
        for (var suffix = 2; taken.Contains(candidate); suffix++)
        {
            var tail = "-" + suffix.ToString(CultureInfo.InvariantCulture);
            candidate = (seed.Length + tail.Length > MaxIdLength ? seed[..(MaxIdLength - tail.Length)] : seed) + tail;
        }

        return candidate;
    }

    /// <summary>Returns the trimmed text, or <see langword="null"/> when it is blank.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The trimmed text or <see langword="null"/>.</returns>
    internal static string? NullIfBlank(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
