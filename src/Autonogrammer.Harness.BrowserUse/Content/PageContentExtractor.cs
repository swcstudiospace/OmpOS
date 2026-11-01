using System.Text;

namespace Autonogrammer.Harness.BrowserUse.Content;

/// <summary>Visibility filter, invisible-Unicode strip and the character cap. Every result stays untrusted.</summary>
public static class PageContentExtractor
{
    /// <summary>The default character cap.</summary>
    public const int DefaultCap = 20000;

    /// <summary>Gets a value indicating that page text is untrusted. Local pages stay untrusted.</summary>
    public static bool IsUntrusted => true;

    /// <summary>Extracts visible text. Hidden CSS, attributes, comments, meta and noscript are dropped.</summary>
    /// <param name="nodes">The fixture or snapshot nodes.</param>
    /// <param name="cap">The character cap.</param>
    /// <returns>The capped text.</returns>
    public static string Extract(IReadOnlyList<PageExtractNode> nodes, int cap = DefaultCap)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (cap < 1)
        {
            cap = DefaultCap;
        }

        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            if (!node.Visible || node.Kind != PageTextKind.Text || string.IsNullOrEmpty(node.Text))
            {
                continue;
            }

            var piece = SensitiveFieldClassifier.MaskIfSensitive(StripInvisible(node.Text), node.Sensitive);
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            var room = cap - builder.Length;
            if (room <= 0)
            {
                break;
            }

            builder.Append(piece.Length <= room ? piece : piece[..room]);
        }

        return builder.ToString();
    }

    /// <summary>Strips zero-width, bidi and tag-block characters.</summary>
    /// <param name="text">The raw text.</param>
    /// <returns>The stripped text.</returns>
    public static string StripInvisible(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsInvisible(rune))
            {
                continue;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    /// <summary>Returns whether a node fails the visibility filter.</summary>
    /// <param name="area">The CSS area.</param>
    /// <param name="display">The display value.</param>
    /// <param name="visibility">The visibility value.</param>
    /// <param name="opacity">The opacity.</param>
    /// <param name="fontPx">The font size in pixels.</param>
    /// <param name="farOffPage">Whether the node is far outside the page.</param>
    /// <returns>True when the node is dropped.</returns>
    public static bool IsHidden(double area, string? display, string? visibility, double opacity, double fontPx, bool farOffPage) =>
        area <= 0
        || string.Equals(display, "none", StringComparison.OrdinalIgnoreCase)
        || string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase)
        || opacity < 0.1
        || (fontPx > 0 && fontPx < 4)
        || farOffPage;

    private static bool IsInvisible(Rune rune)
    {
        var value = rune.Value;
        return value is 0x200B or 0x200C or 0x200D or 0x2060 or 0xFEFF or 0x180E
            or (>= 0x200E and <= 0x200F)
            or (>= 0x202A and <= 0x202E)
            or (>= 0x2066 and <= 0x2069)
            or (>= 0xE0000 and <= 0xE007F);
    }
}
