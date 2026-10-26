using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// Builds the small controls the block views share: text, headings, marked rows, code text and disclosures.
/// Each class name is a hook for tests and styles; text is named by its content, so no control here has a separate name.
/// </summary>
internal static class BlockControls
{
    /// <summary>Creates a wrapping text block.</summary>
    /// <param name="text">The text, shown verbatim.</param>
    /// <param name="cssClass">An optional class name.</param>
    /// <returns>The text block.</returns>
    public static TextBlock Text(string text, string? cssClass = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        if (cssClass is not null)
        {
            block.Classes.Add(cssClass);
        }

        return block;
    }

    /// <summary>Creates a wrapping monospace text block for code or raw content.</summary>
    /// <param name="text">The code text, shown verbatim.</param>
    /// <returns>The text block.</returns>
    public static TextBlock Code(string text)
    {
        var block = Text(text);
        block.FontFamily = BlockTokens.Monospace;
        return block;
    }

    /// <summary>Gets the inline collection of a text block, which every text block has.</summary>
    /// <param name="block">The text block.</param>
    /// <returns>The block's inline collection.</returns>
    /// <exception cref="InvalidOperationException">The text block has no inline collection.</exception>
    public static InlineCollection InlinesOf(TextBlock block) =>
        block.Inlines ?? throw new InvalidOperationException("A text block always has an inline collection.");

    /// <summary>Gives a text block the heading size and semibold weight.</summary>
    /// <param name="block">The text block to style.</param>
    /// <returns>The same text block.</returns>
    public static TextBlock AsHeading(TextBlock block)
    {
        block.FontSize = BlockTokens.Get<double>("OmpOS.Font.Size.Heading");
        block.FontWeight = BlockTokens.Get<FontWeight>("OmpOS.Font.Weight.Semibold");
        return block;
    }

    /// <summary>Creates a list row: a marker column beside the row's content.</summary>
    /// <param name="marker">The marker, such as a bullet or a step number.</param>
    /// <param name="content">The row's content.</param>
    /// <returns>The row.</returns>
    public static Grid Row(string marker, Control content)
    {
        var markerText = Text(marker);
        markerText.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(markerText, 0);
        Grid.SetColumn(content, 1);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = BlockTokens.Get<double>("OmpOS.Gap.Sm"),
        };
        row.Children.Add(markerText);
        row.Children.Add(content);
        return row;
    }

    /// <summary>Creates a collapsed disclosure with a header and content.</summary>
    /// <param name="header">The header text, which is also the disclosure's accessible name.</param>
    /// <param name="content">The content shown when the disclosure is expanded.</param>
    /// <returns>The disclosure, collapsed.</returns>
    public static Expander Disclosure(string header, Control content)
    {
        var expander = new Expander { Header = header, Content = content, IsExpanded = false };
        AutomationProperties.SetName(expander, header);
        return expander;
    }

    /// <summary>Builds an underline for a link. Avalonia's shared decorations belong to the dispatcher that created them, so a fresh one is built for each use.</summary>
    /// <returns>A new underline decoration collection.</returns>
    public static TextDecorationCollection Underline() => new() { new TextDecoration { Location = TextDecorationLocation.Underline } };

    /// <summary>Builds a strikethrough for struck-through text. A fresh instance is built for each use, for the same reason as <see cref="Underline"/>.</summary>
    /// <returns>A new strikethrough decoration collection.</returns>
    public static TextDecorationCollection Strikethrough() => new() { new TextDecoration { Location = TextDecorationLocation.Strikethrough } };
}
