using System.Collections.Frozen;

namespace OmpOS.Workspace.Conventions;

/// <summary>A markdown file split into its frontmatter fields and its body.</summary>
/// <param name="Fields">The top-level keys of the frontmatter block. Empty when the file has none or the block is malformed.</param>
/// <param name="Body">The text after the frontmatter block, or the whole text when the file has none.</param>
/// <param name="Problem">The reason the frontmatter block is malformed, or <see langword="null"/> when it is well formed or absent.</param>
public sealed record ConventionFrontmatter(IReadOnlyDictionary<string, ConventionFieldValue> Fields, string Body, string? Problem)
{
    private static readonly FrozenSet<string> TrueWords = new[] { "true", "yes", "on" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> FalseWords = new[] { "false", "no", "off" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads a scalar key.</summary>
    /// <param name="key">The key, matched exactly.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when the key is absent or empty.</returns>
    public string? ReadText(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Fields.TryGetValue(key, out var value) && value.Text.Length > 0 ? value.Text : null;
    }

    /// <summary>Reads a yes-or-no key (<c>true</c>, <c>yes</c>, <c>on</c> or <c>false</c>, <c>no</c>, <c>off</c>, in any case).</summary>
    /// <param name="key">The key, matched exactly.</param>
    /// <returns>The flag, or <see langword="null"/> when the key is absent or holds any other text.</returns>
    public bool? ReadFlag(string key)
    {
        var text = ReadText(key);
        if (text is null)
        {
            return null;
        }

        if (TrueWords.Contains(text))
        {
            return true;
        }

        return FalseWords.Contains(text) ? false : null;
    }

    /// <summary>Reads a list key. A scalar is split on commas, so <c>globs: **/*.ts,**/*.tsx</c> reads as two items.</summary>
    /// <param name="key">The key, matched exactly.</param>
    /// <returns>The items; empty when the key is absent.</returns>
    public IReadOnlyList<string> ReadList(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Fields.TryGetValue(key, out var value))
        {
            return [];
        }

        if (value.IsList)
        {
            return value.Items;
        }

        return value.Text.Length == 0
            ? []
            : value.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
